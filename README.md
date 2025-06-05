# LabIntroducingChatBot
## 概要

LabIntroducingChatBot は、研究室のオープンラボで展示された対話型チャットボットアプリケーションです。ユーザーからのテキスト入力を受け取り、AI（Ollama）による応答生成と、VOICEVOX Engine による音声合成をリアルタイムで行い、その音声を再生します。このプロジェクトは、自然言語処理、音声合成技術、立体映像の融合を体験できるデモンストレーションとして開発しました。
## 主な機能
ユーザー入力の受付: テキストフィールドを介してユーザーからの質問や発言を受け取ります。

Ollama による応答生成: 入力されたテキストをOllamaを稼働させているサーバーに送信し、AIによる自然な応答文を生成します。

VOICEVOX Engine による音声合成: 生成された応答文をVOICEVOX Engine を動かしているサーバーに送り、高品質な音声ファイルに変換します。

音声再生: 合成された音声ファイルをアプリケーション内で再生し、チャットボットとの対話ができます。

VRM モデルの表示: UniVRM を利用して3Dアバターを表示しています。

Spatial Reality Display 対応: Spatial Reality Display Plugin for Unity を活用し、裸眼で立体視が可能です。
## 開発環境
Unity: 2022.3.62f1
## 使用ライブラリ
このプロジェクトは、以下の主要なUnityライブラリおよび外部連携ライブラリを使用しています。
* UniTask(https://github.com/Cysharp/UniTask)
* VoicevoxClientSharp(https://github.com/TORISOUP/VoicevoxClientSharp)
* UniVRM(https://github.com/vrm-c/UniVRM)
* Spatial Reality Display Plugin for Unity
