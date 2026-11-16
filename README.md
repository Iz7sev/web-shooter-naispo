# Webシューター Naispo

Meta Quest 3のハンドトラッキングを使った展示向けの VR / MR 体験です。[Web Swing Lab 1.14.0](https://github.com/Iz7sev/web-swing-lab) をもとに、短時間の体験とスタッフ向けの操作機能を追加しています。

## 体験内容

- **VR** — ウェブを撃つ、握る、引く、手を開く操作で街を飛び回ります。スタッフはガイド付き上昇飛行、高速ブースト飛行、自由スイングの3段階から選べます。
- **MR** — パススルーで見える部屋を舞台にした60秒の投てきチャレンジです。ウェブを短く引くと木箱が自動で近づきます。的に当てると1投につき最大1点が入り、残り時間と最高記録を確認できます。

## 展示での使い方

両手を認識するとカウントダウンが始まり、プレイ中にハンドトラッキングが途切れると一時停止します。VRは設定した体験時間で終了します。スタッフは待機中に Quest の Touch コントローラーでレベルを選択して開始できます。プレイ中は主ボタンと副ボタンを1.5秒長押しすると終了できます。

Macでは Meta のブラウザーキャストで映像を見ながら、Quest が配信する操作パネルで進行を管理できます。Quest と Mac を同じネットワークに接続し、Quest の待機画面に表示されたアドレス（ポート18765）を開いて、6桁のペアリングコードを入力します。Unity Editor では操作パネルは起動しません。詳しい手順は[展示スタッフ向け運用ガイド](OPERATIONS_JA.md)を参照してください。

## Unity で開いてビルドする

1. Android Build Support（SDK / NDK / OpenJDK）付きの Unity **6000.6.4f1** を用意し、このリポジトリのルートを Unity Hub から開きます。
2. `Naispo > Switch To Android` でビルド先を Android に切り替えます。
3. `Naispo > Run Exhibition Checks` で展示版のチェックを実行します。
4. `Naispo > Build Quest APK` を実行します。出力先は `Builds/WebShooterNaispo.apk` です。

バージョンは **1.2.3**（Android versionCode **6**）、パッケージ名は `com.webswinglab.naispo` です。APK はローカルインストール用で、`com.oculus.app_id` は含みません。Quest の「提供元不明／ローカルアプリ」一覧に表示された記録があります。

街のモデル・素材の出典とライセンスは[素材メモ](ASSET_NOTES.txt)と[第三者ライセンス](Assets/WebSwing/ThirdPartyLicenses)を参照してください。
