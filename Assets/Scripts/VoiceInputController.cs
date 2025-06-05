// VoiceInputController.cs の改良版
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Windows.Speech;
using System.Linq;
using TMPro;

public class VoiceInputController : MonoBehaviour
{
    [Header("UIコンポーネント")]
    public Button recordButton;
    public TextMeshProUGUI statusText;

    [Header("音声認識設定")]
    [SerializeField] private float confidenceThreshold = 0.3f; // 認識の信頼度しきい値
    [SerializeField] private float autoStopDelay = 3f; // 自動停止までの時間

    public event System.Action<string> OnVoiceRecognized;
    private DictationRecognizer dictationRecognizer;
    private bool isRecording = false;
    private string lastRecognizedString = "";
    private string currentHypothesis = ""; // 現在の仮認識結果

    void Start()
    {
        // マイクテスト
        TestMicrophone();
        
        if (recordButton != null)
        {
            recordButton.onClick.AddListener(ToggleRecording);
            UpdateRecordButtonText();
        }
        else Debug.LogError("録音ボタンがVoiceInputControllerに設定されていません。");

        if (statusText != null) statusText.text = "マイクボタンで録音開始";
        else Debug.LogWarning("ステータステキストがVoiceInputControllerに設定されていません。");

        InitializeDictationRecognizer();
    }

    private void TestMicrophone()
    {
        string[] devices = Microphone.devices;
        Debug.Log($"利用可能なマイク数: {devices.Length}");
        
        if (devices.Length > 0)
        {
            for (int i = 0; i < devices.Length; i++)
            {
                Debug.Log($"マイク {i}: {devices[i]}");
                int minFreq, maxFreq;
                Microphone.GetDeviceCaps(devices[i], out minFreq, out maxFreq);
                Debug.Log($"  周波数範囲: {minFreq}Hz - {maxFreq}Hz");
            }
        }
        else
        {
            Debug.LogError("利用可能なマイクが見つかりません");
        }
    }

    private void InitializeDictationRecognizer()
    {
        Debug.Log($"PhraseRecognitionSystem.isSupported: {PhraseRecognitionSystem.isSupported}");
        Debug.Log($"Platform: {Application.platform}");
        
        if (PhraseRecognitionSystem.isSupported)
        {
            try
            {
                dictationRecognizer = new DictationRecognizer();
                Debug.Log("DictationRecognizer作成成功");

                dictationRecognizer.DictationResult += OnDictationResultHandler;
                dictationRecognizer.DictationHypothesis += OnDictationHypothesisHandler;
                dictationRecognizer.DictationComplete += OnDictationCompleteHandler;
                dictationRecognizer.DictationError += OnDictationErrorHandler;
                
                Debug.Log("DictationRecognizerイベント登録完了");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"DictationRecognizer初期化エラー: {e.Message}");
            }
        }
        else
        {
            Debug.LogError("このデバイスでは音声認識がサポートされていません。");
            if (recordButton != null) recordButton.interactable = false;
            if (statusText != null) statusText.text = "音声認識非対応";
        }
    }

    public void ToggleRecording()
    {
        if (isRecording) StopRecording();
        else StartRecording();
    }

    public void StartRecording()
    {
        if (dictationRecognizer == null)
        {
            Debug.LogError("DictationRecognizerが初期化されていません。");
            return;
        }

        Debug.Log($"DictationRecognizer Status: {dictationRecognizer.Status}");

        if (dictationRecognizer.Status == SpeechSystemStatus.Stopped)
        {
            try
            {
                lastRecognizedString = "";
                currentHypothesis = "";
                
                dictationRecognizer.Start();
                isRecording = true;
                
                if (statusText != null) statusText.text = "録音中... はっきりと話してください";
                Debug.Log("音声認識開始成功");
                
                // 自動停止タイマー（オプション）
                Invoke(nameof(AutoStopRecording), autoStopDelay);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"音声認識開始エラー: {e.Message}");
                if (statusText != null) statusText.text = "開始エラー";
                isRecording = false;
            }
        }
        else
        {
            Debug.LogWarning($"DictationRecognizer状態が不正: {dictationRecognizer.Status}");
        }
        UpdateRecordButtonText();
    }

    private void AutoStopRecording()
    {
        if (isRecording)
        {
            Debug.Log("自動停止タイマーにより録音停止");
            StopRecording();
        }
    }

    public void StopRecording()
    {
        CancelInvoke(nameof(AutoStopRecording));
        
        if (dictationRecognizer == null) return;

        if (dictationRecognizer.Status == SpeechSystemStatus.Running)
        {
            Debug.Log("音声認識停止要求");
            dictationRecognizer.Stop();
        }
        
        isRecording = false;
        if (statusText != null) statusText.text = "処理中...";
        UpdateRecordButtonText();
    }

    private void OnDictationResultHandler(string text, ConfidenceLevel confidence)
    {
        Debug.Log($"認識結果: '{text}' (信頼度: {confidence})");
        Debug.Log($"信頼度数値: {(float)confidence}");
        
        // 信頼度チェック
        if ((float)confidence >= confidenceThreshold)
        {
            lastRecognizedString = text;
            if (statusText != null) statusText.text = $"認識成功: {text}";
            
            if (!string.IsNullOrEmpty(text))
            {
                OnVoiceRecognized?.Invoke(text);
                Debug.Log($"音声認識イベント発行: {text}");
            }
        }
        else
        {
            Debug.LogWarning($"信頼度が低いため結果を破棄: {confidence} < {confidenceThreshold}");
            if (statusText != null) statusText.text = $"認識が不明瞭です（再試行してください）";
        }
    }

    private void OnDictationHypothesisHandler(string text)
    {
        currentHypothesis = text;
        Debug.Log($"仮認識: '{text}'");
        if (statusText != null && isRecording) 
        {
            statusText.text = $"認識中: {text}...";
        }
    }

    private void OnDictationCompleteHandler(DictationCompletionCause cause)
    {
        isRecording = false;
        UpdateRecordButtonText();
        CancelInvoke(nameof(AutoStopRecording));

        Debug.Log($"音声認識完了 (原因: {cause})");
        Debug.Log($"最終認識結果: '{lastRecognizedString}'");
        Debug.Log($"最終仮認識: '{currentHypothesis}'");

        if (cause == DictationCompletionCause.Complete)
        {
            if (!string.IsNullOrEmpty(lastRecognizedString))
            {
                if (statusText != null) statusText.text = $"完了: {lastRecognizedString}";
            }
            else if (!string.IsNullOrEmpty(currentHypothesis))
            {
                // 仮認識結果があるが最終結果がない場合
                Debug.LogWarning("仮認識はあったが最終結果なし。信頼度が低かった可能性");
                if (statusText != null) statusText.text = "認識が不明瞭でした。もう一度お試しください";
            }
            else
            {
                Debug.LogWarning("音声が検出されませんでした");
                if (statusText != null) statusText.text = "音声が検出されません。マイクの近くで話してください";
            }
        }
        else
        {
            if (statusText != null) statusText.text = $"認識中断: {cause}";
        }
    }

    private void OnDictationErrorHandler(string error, int hresult)
    {
        isRecording = false;
        UpdateRecordButtonText();
        Debug.LogError($"音声認識エラー: {error} (HResult: {hresult})");
        if (statusText != null) statusText.text = $"エラー: {error.Split('(')[0]}";
    }

    private void UpdateRecordButtonText()
    {
        if (recordButton != null)
        {
            Text buttonText = recordButton.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.text = isRecording ? "録音停止" : "音声入力";
            }
        }
    }

    void OnDestroy()
    {
        if (dictationRecognizer != null)
        {
            dictationRecognizer.DictationResult -= OnDictationResultHandler;
            dictationRecognizer.DictationHypothesis -= OnDictationHypothesisHandler;
            dictationRecognizer.DictationComplete -= OnDictationCompleteHandler;
            dictationRecognizer.DictationError -= OnDictationErrorHandler;

            if (dictationRecognizer.Status == SpeechSystemStatus.Running)
            {
                dictationRecognizer.Stop();
            }
            dictationRecognizer.Dispose();
            dictationRecognizer = null;
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && isRecording)
        {
            StopRecording();
            Debug.Log("フォーカスが外れたため録音停止");
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && isRecording)
        {
            StopRecording();
            Debug.Log("アプリポーズのため録音停止");
        }
    }
}