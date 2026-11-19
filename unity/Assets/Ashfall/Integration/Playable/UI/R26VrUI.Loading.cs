#if UNITY_6000_0_OR_NEWER
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.UI
{
    public sealed partial class R23VrUI
    {
        Vector3 loadingPosition;Quaternion loadingRotation;
        Texture2D loadingTexture;Material loadingMaterial;bool loadingImagePending;
        Renderer loadingLogoRenderer;TextMesh loadingFallback;
        void AnchorLoadingLogo()
        {
            Vector3 forward=Vector3.ProjectOnPlane(view.transform.forward,Vector3.up);
            if(forward.sqrMagnitude<.0001f)forward=Vector3.forward;forward.Normalize();
            loadingPosition=view.transform.position+forward*2.5f;loadingRotation=Quaternion.LookRotation(forward,Vector3.up);
        }
        void BuildLoadingLogo()
        {
            loadingLogoRenderer=Quad(panel,"ASHFALL loading wordmark",Vector3.zero,new Vector2(2.2f,.55f),Color.white).GetComponent<Renderer>();
            loadingLogoRenderer.enabled=false;
            loadingFallback=Text(panel,"ASHFALL",Vector3.zero,.0792f,2.2f,Hex(R25Theme.Text));
            detail=Text(panel,"LOADING",new Vector3(0,-.40f,-.01f),.023f,.98f,Hex(R25Theme.TextDim));
            Quad(panel,"Loading progress track",new Vector3(0,-.47f,0),new Vector2(.98f,.006f),Hex(R25Theme.Raised));
            progressFill=Quad(panel,"Loading progress remaining",new Vector3(0,-.47f,-.002f),new Vector2(1,.006f),Hex(R25Theme.Teal));SetBar(progressFill,progress,.98f);
            if(loadingTexture!=null)ApplyLoadingLogo();else if(!loadingImagePending)StartCoroutine(LoadLoadingLogo());
        }
        void ApplyLoadingLogo()
        {
            if(Page!=R23UiPage.Loading||loadingLogoRenderer==null)return;
            // Preserve the real asset aspect ratio within the same title-wordmark footprint.
            float ratio=(float)loadingTexture.width/loadingTexture.height;
            loadingLogoRenderer.transform.localScale=new Vector3(Mathf.Min(1,.55f*ratio/2.2f),Mathf.Min(1,2.2f/ratio/.55f),1);
            loadingLogoRenderer.sharedMaterial=loadingMaterial;loadingLogoRenderer.enabled=true;
            if(loadingFallback!=null)loadingFallback.gameObject.SetActive(false);
        }
        IEnumerator LoadLoadingLogo()
        {
            loadingImagePending=true;
            try
            {
                using(var request=UnityWebRequest.Get(BrandingUrl("logo-wordmark.png")))
                {
                    request.timeout=10;yield return request.SendWebRequest();
                    if(request.result!=UnityWebRequest.Result.Success)yield break;
                    loadingTexture=new Texture2D(2,2,TextureFormat.RGBA32,false,false);
                    if(!loadingTexture.LoadImage(request.downloadHandler.data,true)){Destroy(loadingTexture);loadingTexture=null;yield break;}
                    loadingTexture.wrapMode=TextureWrapMode.Clamp;
                    loadingMaterial=new Material(Resources.Load<Shader>("R23SourceIcon"));loadingMaterial.SetTexture("_IconTex",loadingTexture);loadingMaterial.renderQueue=4102;materials.Add(loadingMaterial);
                    ApplyLoadingLogo();
                }
            }
            finally{loadingImagePending=false;}
        }
    }
}
#endif
