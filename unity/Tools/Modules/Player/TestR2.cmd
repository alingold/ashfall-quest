@echo off
setlocal
set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "ST=C:\Users\aling\Documents\ashfall\campaign-staging\20261104-three-chats\unity\Player\r2"
for %%N in (BuilderTests Adversarial R2Tests) do (
  "%CSC%" "@%ST%\%%N.rsp"
  if errorlevel 1 exit /b 1
  "%ST%\%%N.exe"
  if errorlevel 1 exit /b 1
)
exit /b 0
