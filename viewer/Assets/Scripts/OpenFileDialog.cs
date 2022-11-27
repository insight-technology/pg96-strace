/// https://github.com/gkngkc/UnityStandaloneFileBrowser
/// https://zenn.dev/plumchang/articles/9187928bcbcf93

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SFB;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class OpenFileDialog : MonoBehaviour, IPointerDownHandler
{

#if UNITY_WEBGL && !UNITY_EDITOR
    //
    // WebGL
    //

    // StandaloneFileBrowserのブラウザスクリプトプラグインから呼び出す
    [DllImport("__Internal")]
    private static extern void UploadFile(string gameObjectName, string methodName, string filter, bool multiple);

    // ファイルを開く
    public void OnPointerDown(PointerEventData eventData) {
        UploadFile(gameObject.name, "OnFileUpload", ".", false);
    }

    // ファイルアップロード後の処理
    public void OnFileUpload(string url) {
        StartCoroutine(Load(url));
    }

#else
    //
    // OSビルド & Unity editor上
    //
    public void OnPointerDown(PointerEventData eventData) { }

    void Start()
    {
        var button = GetComponent<Button>();
        button.onClick.AddListener(() => OpenFile());
    }

    // ファイルを開く
    public void OpenFile()
    {
        // 拡張子フィルタ
        var extensions = new[] {
            new ExtensionFilter("All Files", "*" ),
        };

        // ファイルダイアログを開く
        var paths = StandaloneFileBrowser.OpenFilePanel("Open File", "", extensions, false);
        if (paths.Length > 0 && paths[0].Length > 0)
        {
            StartCoroutine(Load(new System.Uri(paths[0]).AbsoluteUri));
        }
    }

#endif

    private IEnumerator Load(string url)
    {
        var request = UnityWebRequest.Get(url);

        var operation = request.SendWebRequest();
        while (!operation.isDone)
        {
            yield return null;
        }

        ContextManager.instance.LoadNewFile(request.downloadHandler.text);        
    }
}
