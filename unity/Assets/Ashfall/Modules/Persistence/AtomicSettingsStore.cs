using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Ashfall.Core;

namespace Ashfall.Modules.Persistence
{
    public sealed class PhysicalSettingsFiles : IAtomicSettingsFiles
    {
        public bool Exists(string path)
        {
            try { File.GetAttributes(path); return true; }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
        }
        public IDisposable AcquireFamily(string destination)
        {
            // Persistent lock file: deleting it can split ownership between handles.
            return new FileStream(destination + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        public byte[] Read(string path, int maxBytes)
        {
            if (maxBytes < 0) throw new ArgumentOutOfRangeException("maxBytes");
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long length = stream.Length;
                if (length > maxBytes) throw new IOException("Settings byte limit exceeded");
                byte[] bytes = new byte[(int)length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int count = stream.Read(bytes, offset, bytes.Length - offset);
                    if (count == 0) throw new IOException("File truncated during read");
                    offset += count;
                }
                if (stream.ReadByte() != -1) throw new IOException("File grew during read");
                return bytes;
            }
        }
        public void CreateDurable(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        }
        public void Commit(string pending, string destination, string backup, bool replacing)
        {
            if (replacing) File.Replace(pending, destination, backup);
            else File.Move(pending, destination);
        }
    }

    // Values are opaque strings. Godot migration decodes strings and retains other values as JSON.
    // No implicit load/default write, no delete-before-move fallback, no Godot path writes.
    public sealed class AtomicSettingsStore : ISettingsStore
    {
        public const int MaxDocumentBytes = 4194304;
        public const int MaxArtifactBytes = 33554432;
        private readonly object gate = new object();
        private readonly string path;
        private readonly IAtomicSettingsFiles files;
        private Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
        private Dictionary<string, string> envelope = new Dictionary<string, string>(StringComparer.Ordinal);
        private string observed;
        private bool loaded, recovering;
        public SettingsState State { get; private set; }
        public string Error { get; private set; }
        public string Path { get { return path; } }
        public AtomicSettingsStore(string path) : this(path, new PhysicalSettingsFiles()) { }
        public AtomicSettingsStore(string path, IAtomicSettingsFiles files)
        {
            if (String.IsNullOrWhiteSpace(path) || files == null) throw new ArgumentException("Path/files required");
            this.path = System.IO.Path.GetFullPath(path); this.files = files; Error = "";
        }
        public RuleResult Load(bool allowBackupRecovery)
        {
            lock (gate)
            {
                try { using (files.AcquireFamily(path)) return LoadOwned(allowBackupRecovery); }
                catch (Exception ex) { loaded = false; return Fail(SettingsState.IoError, ex.Message); }
            }
        }
        private RuleResult LoadOwned(bool allowBackupRecovery)
        {
            loaded = false; recovering = false; values.Clear(); envelope.Clear();
            try
            {
                byte[] primary = files.Exists(path) ? files.Read(path, MaxDocumentBytes) : null;
                observed = primary == null ? null : Hash(primary);
                Dictionary<string, string> root, data;
                SettingsState primaryState = Inspect(primary, out root, out data);
                // Future backup must also block writes: never downgrade a newer save family.
                byte[] backup = files.Exists(path + ".backup") ? files.Read(path + ".backup", MaxDocumentBytes) : null;
                Dictionary<string, string> backupRoot, backupData;
                SettingsState backupState = Inspect(backup, out backupRoot, out backupData);
                if (primaryState == SettingsState.NewerSchema || backupState == SettingsState.NewerSchema)
                    return Fail(SettingsState.NewerSchema, "Newer schema in save family");
                State = primaryState;
                if (State == SettingsState.Ready) { envelope = root; values = data; }
                else if (allowBackupRecovery && backupState == SettingsState.Ready)
                { envelope = backupRoot; values = backupData; State = SettingsState.Recovered; recovering = true; }
                else if (State != SettingsState.Missing) return Fail(State, "Invalid settings; recovery requires explicit valid backup");
                else if (backup != null) return Fail(SettingsState.Corrupt, "Orphan backup requires explicit recovery");
                loaded = true; Error = ""; return RuleResult.Accept();
            }
            catch (Exception ex) { return Fail(SettingsState.IoError, ex.Message); }
        }
        public bool TryRead(string key, out string value) { lock (gate) return values.TryGetValue(key, out value); }
        public void Write(string key, string value)
        { lock (gate) WriteOwned(key, value); }
        private void WriteOwned(string key, string value)
        {
            if (!loaded) throw new InvalidOperationException("Successful Load required");
            if (String.IsNullOrEmpty(key) || value == null) throw new ArgumentException("Nonempty key and nonnull value required");
            JsonTokens.ValidateUnicode(key); JsonTokens.ValidateUnicode(value);
            values[key] = value;
        }
        public RuleResult Flush()
        {
            lock (gate)
            {
                try { using (files.AcquireFamily(path)) return FlushOwned(); }
                catch (Exception ex) { Error = ex.Message; return RuleResult.Reject("Ownership/flush failed: " + ex.Message); }
            }
        }
        private RuleResult FlushOwned()
        {
            if (!loaded) return RuleResult.Reject("Successful Load required");
            try
            {
                byte[] current = files.Exists(path) ? files.Read(path, MaxDocumentBytes) : null;
                if ((current == null ? null : Hash(current)) != observed) return RuleResult.Reject("Settings changed externally; reload required");
                if (files.Exists(path + ".backup"))
                {
                    Dictionary<string, string> a, b;
                    if (Inspect(files.Read(path + ".backup", MaxDocumentBytes), out a, out b) == SettingsState.NewerSchema)
                        return RuleResult.Reject("Newer backup schema");
                }
                var encoded = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var pair in values) encoded.Add(pair.Key, JsonTokens.Quote(pair.Value));
                var next = new Dictionary<string, string>(envelope, StringComparer.Ordinal);
                next["schema"] = "1"; next["values"] = JsonTokens.Encode(encoded);
                byte[] bytes = Utf8.GetBytes(JsonTokens.Encode(next));
                Dictionary<string, string> checkRoot, checkData;
                if (Inspect(bytes, out checkRoot, out checkData) != SettingsState.Ready) return RuleResult.Reject("Settings size/encoding invalid");
                if (recovering && current != null) Preserve(path + ".corrupt." + Hash(current), current);
                string pending = path + ".pending." + Guid.NewGuid().ToString("N");
                files.CreateDurable(pending, bytes);
                if (Hash(files.Read(pending, MaxDocumentBytes)) != Hash(bytes)) return RuleResult.Reject("Pending verification failed");
                // Defense against observable uncooperative mutations during pending creation.
                // The family lock, not this recheck, supplies cooperative serialization.
                byte[] latest = files.Exists(path) ? files.Read(path, MaxDocumentBytes) : null;
                if ((latest == null ? null : Hash(latest)) != observed) return RuleResult.Reject("Settings changed before commit");
                if (files.Exists(path + ".backup"))
                {
                    Dictionary<string, string> a, b;
                    if (Inspect(files.Read(path + ".backup", MaxDocumentBytes), out a, out b) == SettingsState.NewerSchema)
                        return RuleResult.Reject("Unsupported backup before commit");
                }
                files.Commit(pending, path, recovering ? null : path + ".backup", current != null);
                observed = Hash(bytes); envelope = next; State = SettingsState.Ready; recovering = false; Error = "";
                return RuleResult.Accept();
            }
            catch (Exception ex) { Error = ex.Message; return RuleResult.Reject("Flush failed: " + ex.Message); }
        }
        // Caller selects an actual Godot user-data file. No OS-specific guessed location.
        // Existing Unity settings always win; migration is only admitted into a missing family.
        public RuleResult ImportGodotSettings(string sourcePath)
        {
            lock (gate)
            {
                try { using (files.AcquireFamily(path)) return ImportOwned(sourcePath); }
                catch (Exception ex) { Error = ex.Message; return RuleResult.Reject("Ownership/import failed: " + ex.Message); }
            }
        }
        private RuleResult ImportOwned(string sourcePath)
        {
            if (!loaded || State != SettingsState.Missing || values.Count != 0) return RuleResult.Reject("Migration requires empty missing store");
            try
            {
                string source = System.IO.Path.GetFullPath(sourcePath);
                if (String.Equals(source, path, StringComparison.OrdinalIgnoreCase)) return RuleResult.Reject("Source equals destination");
                byte[] original = files.Read(source, MaxDocumentBytes);
                var raw = JsonTokens.Object(Decode(original));
                string version;
                if (raw.TryGetValue("schema", out version) || raw.TryGetValue("version", out version))
                    return RuleResult.Reject("Versioned source requires an explicit migration");
                var imported = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var pair in raw) imported.Add(pair.Key, pair.Value.StartsWith("\"", StringComparison.Ordinal) ? JsonTokens.String(pair.Value) : pair.Value);
                // Sidecar is content-addressed and immutable, including unknown keys and exact bytes.
                string sidecar = path + ".godot." + Hash(original) + ".json";
                var metadata = new Dictionary<string, string>();
                metadata["source"] = JsonTokens.Quote(source); metadata["sha256"] = JsonTokens.Quote(Hash(original));
                metadata["originalBase64"] = JsonTokens.Quote(Convert.ToBase64String(original));
                metadata["rawValues"] = JsonTokens.Encode(raw);
                Preserve(sidecar, Utf8.GetBytes(JsonTokens.Encode(metadata)));
                values = imported; envelope["godotSidecar"] = JsonTokens.Quote(sidecar);
                return RuleResult.Accept(); // Explicit Flush commits; failures leave source and sidecar intact.
            }
            catch (Exception ex) { Error = ex.Message; return RuleResult.Reject("Migration failed: " + ex.Message); }
        }
        private void Preserve(string name, byte[] bytes)
        {
            if (!files.Exists(name)) files.CreateDurable(name, bytes);
            if (Hash(files.Read(name, MaxArtifactBytes)) != Hash(bytes)) throw new IOException("Preserved artifact mismatch");
        }
        private RuleResult Fail(SettingsState state, string error) { State = state; Error = error; return RuleResult.Reject(error); }
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static string Decode(byte[] bytes)
        { string text = Utf8.GetString(bytes); return text.Length > 0 && text[0] == '\ufeff' ? text.Substring(1) : text; }
        internal static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static bool IsSchemaOne(string token)
        {
            // JSON grammar already validated. Compare the exact coefficient and decimal
            // exponent; never convert through a finite precision numeric value.
            if (token.Length == 0 || token[0] < '0' || token[0] > '9') return false;
            int e = token.IndexOfAny(new[] { 'e', 'E' });
            string coefficient = e < 0 ? token : token.Substring(0, e);
            long exponent = 0;
            if (e >= 0 && !Int64.TryParse(token.Substring(e + 1),
                System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture, out exponent)) return false;
            int dot = coefficient.IndexOf('.');
            int fraction = dot < 0 ? 0 : coefficient.Length - dot - 1;
            string digits = coefficient.Replace(".", "").TrimStart('0');
            int end = digits.Length;
            while (end > 0 && digits[end - 1] == '0') end--;
            if (end != 1 || digits[0] != '1') return false;
            // Compare without exponent arithmetic that could overflow.
            return exponent == (long)fraction - (digits.Length - end);
        }
        private static SettingsState Inspect(byte[] bytes, out Dictionary<string, string> root, out Dictionary<string, string> data)
        {
            root = null; data = null;
            if (bytes == null) return SettingsState.Missing;
            try
            {
                if (bytes.Length > MaxDocumentBytes) return SettingsState.Corrupt;
                root = JsonTokens.Object(Decode(bytes)); string schema;
                if (!root.TryGetValue("schema", out schema)) return SettingsState.NewerSchema;
                // Unsupported/unknown versions are blocked, never eligible for recovery.
                if (!IsSchemaOne(schema)) return SettingsState.NewerSchema;
                data = JsonTokens.Object(root["values"]);
                foreach (string key in new List<string>(data.Keys)) data[key] = JsonTokens.String(data[key]);
                return SettingsState.Ready;
            }
            catch (Exception) { return SettingsState.Corrupt; }
        }
    }
}
