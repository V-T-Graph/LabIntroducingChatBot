using System.Threading;
using Cysharp.Threading.Tasks; // UniTaskが必須
using UnityEngine;
using VoicevoxClientSharp;
using VoicevoxClientSharp.Unity;

namespace Sandbox
{
    // 使用例
    public class Sample : MonoBehaviour
    {
        // 設定済みのVoicevoxSpeakPlayerをバインドしておく
        [SerializeField] 
        private VoicevoxSpeakPlayer _voicevoxSpeakPlayer;

        // VoicevoxSynthesizerを用いて音声合成する
        private readonly VoicevoxSynthesizer _voicevoxSynthesizer 
            = new VoicevoxSynthesizer();

        private void Start()
        {
            var cancellationToken = this.GetCancellationTokenOnDestroy();

            // テキストを音声に変換して再生する
            SpeakAsync("こんにちは、世界", cancellationToken).Forget();
        }

        // 音声合成して再生する処理
        private async UniTask SpeakAsync(string text, CancellationToken ct)
        {
            // テキストをVoicevoxで音声合成
            var synthesisResult = await _voicevoxSynthesizer.SynthesizeSpeechAsync(
                0, text, cancellationToken: ct);
            
            // 結果を再生する
            await _voicevoxSpeakPlayer.PlayAsync(synthesisResult, ct);
        }

        private void OnDestroy()
        {
            _voicevoxSynthesizer.Dispose();
        }
    }
}
