// ConversationManager.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using VRM; // VRMBlendShapeProxy, BlendShapeKey, BlendShapePreset を使うために必要
using VoicevoxClientSharp;
using VoicevoxClientSharp.Unity;

/// <summary>
/// 会話全体を管理するクラス (Ollama連携、VRM対応、VoicevoxClientSharp連携版)
/// </summary>
public class ConversationManager : MonoBehaviour
{
    [Header("UIコンポーネント (TextMeshPro)")]
    public TMP_InputField userInputField;
    public TextMeshProUGUI aiResponseText;
    public Button sendButton;

    [Header("キャラクター制御")]
    public GameObject characterObject; // VRMモデルのGameObject
    private VRMBlendShapeProxy vrmBlendShapeProxy; // VRMの表情制御用

    [Header("言語モデルサービス")]
    private ILanguageModelService languageModelService;
    [Header("外部コンポーネント")]
    public VoiceInputController voiceInputController;
    [SerializeField] private VoicevoxSpeakPlayer voicevoxSpeakPlayer;

    [Header("VOICEVOX設定")]
    public int speakerId = 1; // VoicevoxClientSharpのspeakerIdはuint型

    [Header("Ollama設定")]
    public string ollamaApiUrl = "http://localhost:11434/api/chat"; // SSHポートフォワード先のURLとエンドポイント
    public string ollamaModelName = "gemma3:latest"; // 使用するOllamaモデル名 (例: "llama3", "gemma:latest" など)


    [Header("VRM表情設定")]
    private List<string> vrmEmotionalExpressions = new List<string> {
        "Joy", "Angry", "Sorrow", "Fun", "Neutral"
    };
    private string currentEmotion = "Neutral";

    private VoicevoxSynthesizer voicevoxSynthesizer;
    private CancellationTokenSource lifeTimeCancellationTokenSource;

    void Awake()
    {
        lifeTimeCancellationTokenSource = new CancellationTokenSource();
        
        // VoicevoxSynthesizerの初期化 (ホストURLはVoicevoxClientSharpのデフォルトまたは設定に従う)
        // VoicevoxClientSharp v0.7.0以降ではコンストラクタでホストを指定する必要がなくなりました。
        // 必要であれば、 new VoicevoxSynthesizer("http://localhost:50021"); のように指定します。
        voicevoxSynthesizer = new VoicevoxSynthesizer(); 

        // キャラクターコンポーネントの取得
        if (characterObject != null)
        {
            vrmBlendShapeProxy = characterObject.GetComponent<VRMBlendShapeProxy>();
            if (vrmBlendShapeProxy == null)
            {
                Debug.LogError("キャラクターオブジェクトに VRMBlendShapeProxy コンポーネントがアタッチされていません。");
            }
        }
        else Debug.LogError("キャラクターオブジェクトが未設定です。");

        if (voicevoxSpeakPlayer == null)
            Debug.LogError("VoicevoxSpeakPlayerがインスペクターから設定されていません。");

        // --- 言語モデルサービスをOllamaLMServiceに変更 ---
        // インスペクターから設定されたURLとモデル名を使用してOllamaLMServiceを初期化
        languageModelService = new OllamaLMService(ollamaApiUrl, ollamaModelName);
        Debug.Log($"OllamaLMServiceが初期化されました。API URL: {ollamaApiUrl}, Model: {ollamaModelName}");
        Debug.LogWarning("OllamaLMServiceを使用する際は、OllamaサーバーおよびSSHトンネルが正しく起動・設定されていることを確認してください。");


        if (userInputField == null || aiResponseText == null || sendButton == null)
            Debug.LogError("UIコンポーネント（TMP版含む）が未設定です。");
        else
        {
            aiResponseText.text = "テキストフィールドに文字を打ってね!";
            sendButton.onClick.AddListener(OnSendButtonClicked);
        }

        if (voiceInputController != null)
            voiceInputController.OnVoiceRecognized += HandleVoiceRecognitionResult;
        else Debug.LogWarning("VoiceInputControllerが未設定です。");
        
        ChangeCharacterExpression("Neutral");
    }

    void OnSendButtonClicked()
    {
        string userInput = userInputField.text;
        if (string.IsNullOrEmpty(userInput)) return;
        userInputField.text = "";
        ProcessUserInput(userInput);
    }

    private void HandleVoiceRecognitionResult(string recognizedText)
    {
        if (!string.IsNullOrEmpty(recognizedText))
        {
            Debug.Log($"ConversationManager: 音声認識結果「{recognizedText}」を受信。");
            ProcessUserInput(recognizedText);
        }
        else Debug.LogWarning("ConversationManager: 空の音声認識結果を受信。");
    }

    public void ProcessUserInput(string userInput)
    {
        if (languageModelService == null)
        {
            Debug.LogError("LanguageModelServiceが初期化されていません。");
            aiResponseText.text = "エラー: 言語モデルサービスがありません。";
            return;
        }
        Debug.Log($"ユーザー入力: 「{userInput}」を処理します。");
        aiResponseText.text = $"あなた: {userInput}";

        // UIを更新して「考え中...」などを表示する (オプション)
        string thinkingMessage = "\nAI: 考え中...";
        aiResponseText.text += thinkingMessage;


        StartCoroutine(languageModelService.GetResponse(userInput, (response) =>
        {
            int thinkingMessageIndex = aiResponseText.text.LastIndexOf(thinkingMessage);
            if(thinkingMessageIndex >= 0)
            {
                aiResponseText.text = aiResponseText.text.Substring(0, thinkingMessageIndex);
            }
            aiResponseText.text += $"\nAI: {response}";

            SpeakTextWithVoicevoxClientSharpAsync(response, lifeTimeCancellationTokenSource.Token).Forget();

            if (vrmEmotionalExpressions.Count > 1) 
            {
                string randomEmotion;
                do {
                    randomEmotion = vrmEmotionalExpressions[Random.Range(0, vrmEmotionalExpressions.Count)];
                } while (randomEmotion == currentEmotion && vrmEmotionalExpressions.Count > 1); 
                
                StartCoroutine(ChangeExpressionAfterDelay(randomEmotion, 0.2f));
            }
        }));
    }
    
    private IEnumerator ChangeExpressionAfterDelay(string expressionKey, float delay)
    {
        yield return new WaitForSeconds(delay);
        ChangeCharacterExpression(expressionKey);
    }

    private async UniTaskVoid SpeakTextWithVoicevoxClientSharpAsync(string textToSpeak, CancellationToken ct)
    {
        if (voicevoxSpeakPlayer == null || voicevoxSynthesizer == null)
        {
            Debug.LogError("VoicevoxSpeakPlayerまたはVoicevoxSynthesizerが利用できません。");
            return;
        }
        if (string.IsNullOrEmpty(textToSpeak)) return;

        Debug.Log($"VoicevoxClientSharpにリクエスト: SpeakerID={speakerId}, Text='{textToSpeak}'");
        try
        {
            var synthesisResult = await voicevoxSynthesizer.SynthesizeSpeechAsync(speakerId, textToSpeak,  speedScale: 1.3M, cancellationToken: ct);
            if (ct.IsCancellationRequested)
            {
                Debug.Log("音声合成がキャンセルされました。");
                voicevoxSynthesizer.Dispose(); // 結果がnullでない場合、Disposeを試みる
                return;
            }

            if (voicevoxSpeakPlayer.AudioSource != null && voicevoxSpeakPlayer.AudioSource.isPlaying)
            {
                 voicevoxSpeakPlayer.AudioSource.Stop();
            }
            await voicevoxSpeakPlayer.PlayAsync(synthesisResult, ct); // synthesisResultはここで消費される(Disposeされる)
            Debug.Log("VoicevoxClientSharpでの音声再生が要求されました。");
        }
        catch (System.OperationCanceledException) { Debug.Log("音声合成または再生がキャンセルされました。"); }
        // catch (VoicevoxAPIException apiEx) { Debug.LogError($"Voicevox APIエラー: {apiEx.Message}\nStatusCode: {apiEx.StatusCode}\nDetail: {apiEx.Detail?.DetailMsg}");}
        catch (System.Exception ex) { Debug.LogError($"VoicevoxClientSharp 音声合成・再生エラー: {ex.Message}"); }
    }

    public void ChangeCharacterExpression(string expressionKey)
    {
        if (vrmBlendShapeProxy == null)
        {
            Debug.LogError("VRMBlendShapeProxyが見つかりません。表情を変更できません。");
            return;
        }

        foreach (var keyStr in vrmEmotionalExpressions)
        {
            if (keyStr == "Neutral") continue; 

            if (System.Enum.TryParse<BlendShapePreset>(keyStr, true, out BlendShapePreset preset))
            {
                vrmBlendShapeProxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(preset), 0f);
            }
            else
            {
                // カスタム名の場合、BlendShapeKeyを直接文字列で作成
                vrmBlendShapeProxy.ImmediatelySetValue(BlendShapeKey.CreateUnknown(keyStr), 0f);
            }
        }

        currentEmotion = expressionKey; 
        if (expressionKey == "Neutral" || string.IsNullOrEmpty(expressionKey))
        {
            Debug.Log("表情をニュートラルにリセットしました。");
        }
        else
        {
            if (System.Enum.TryParse<BlendShapePreset>(expressionKey, true, out BlendShapePreset targetPreset))
            {
                vrmBlendShapeProxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(targetPreset), 1.0f);
            }
            else
            {
                vrmBlendShapeProxy.ImmediatelySetValue(BlendShapeKey.CreateUnknown(expressionKey), 1.0f);
            }
        }
    }

    void OnDestroy()
    {
        if (voiceInputController != null)
            voiceInputController.OnVoiceRecognized -= HandleVoiceRecognitionResult;

        if (lifeTimeCancellationTokenSource != null)
        {
            lifeTimeCancellationTokenSource.Cancel();
            lifeTimeCancellationTokenSource.Dispose();
            lifeTimeCancellationTokenSource = null;
        }

        if (voicevoxSynthesizer != null)
        {
            voicevoxSynthesizer.Dispose();
            voicevoxSynthesizer = null;
        }
    }
}

// --- ILanguageModelService インターフェースと OllamaLMService の実装 (以前の回答と同じものを使用) ---
// (PlaceholderLMService はここでは不要になりますが、テスト用に残しておいても構いません)

public interface ILanguageModelService { IEnumerator GetResponse(string userInput, System.Action<string> callback); }


// OllamaLMService (実際のLLM連携用)
public class OllamaLMService : ILanguageModelService
{
    private string ollamaApiUrl;
    private string modelName;
    public OllamaLMService(string apiUrl, string model)
    {
        this.ollamaApiUrl = apiUrl;
        this.modelName = model;
        Debug.Log($"OllamaLMService initialized. API URL: {this.ollamaApiUrl}, Model: {this.modelName}");
    }

    public IEnumerator GetResponse(string userInput, System.Action<string> callback)
    {
        Debug.Log($"OllamaLMService: 「{userInput}」をOllama ({ollamaApiUrl}, model: {modelName})に送信中...");

        var messages = new List<OllamaMessageContent>();

        messages.Add(new OllamaMessageContent { role = "user", content = userInput });
        // TODO: 必要に応じて会話履歴を messages に追加する処理

        var requestData = new OllamaChatRequest
        {
            model = this.modelName,
            messages = messages,
            stream = false
        };
        string jsonPayload = JsonUtility.ToJson(requestData);

        using (UnityEngine.Networking.UnityWebRequest webRequest = new UnityEngine.Networking.UnityWebRequest(ollamaApiUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            webRequest.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Ollama APIエラー: {webRequest.error}\nURL: {ollamaApiUrl}\nResponse: {webRequest.downloadHandler.text}\nSSHトンネルやOllamaサーバーの状態を確認してください。");
                callback("AIとの接続に問題が発生しました。しばらくしてからもう一度お試しください。");
            }
            else
            {
                Debug.Log($"Ollama API Raw Response: {webRequest.downloadHandler.text}");
                try
                {
                    var parsedJson = JsonUtility.FromJson<OllamaChatResponseWrapper>(webRequest.downloadHandler.text);
                    if (parsedJson != null && parsedJson.message != null && !string.IsNullOrEmpty(parsedJson.message.content))
                    {
                        callback(parsedJson.message.content.Trim()); // Trimで前後の空白を除去
                    }
                    else
                    {
                        // フォールバックや他の形式のパース処理 (必要であれば)
                        Debug.LogError("Ollama応答のパースに失敗 (message.contentが見つからないか空です)。応答構造を確認してください。");
                        callback("AIの応答をうまく理解できませんでした。");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Ollama応答のJSONパース中に例外発生: {ex.Message}\nRaw Response: {webRequest.downloadHandler.text}");
                    callback("AIの応答形式が予期したものと異なりました。");
                }
            }
        }
    }



   
    // Ollama APIリクエスト/レスポンス用の補助クラス
    [System.Serializable]
    private class OllamaMessageContent
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    private class OllamaChatRequest
    {
        public string model;
        public List<OllamaMessageContent> messages;
        public bool stream;
        // public string format; // e.g., "json"
        // public OllamaOptions options; // 必要に応じて追加
    }

    [System.Serializable]
    private class OllamaChatResponseWrapper
    {
        public string model;
        public string created_at;
        public OllamaMessageContent message;
        public bool done;
        // ... total_duration, load_duration, etc.
    }
}
