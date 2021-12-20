using System;
using System.Drawing;
using System.Windows.Forms;

namespace Client   // この行は変更禁止
{
    class Program : System.Windows.Forms.Form
    {
        private System.Windows.Forms.TextBox ServerBox, PortBox, SendBox, ReceiveBox; // テキストボックス
        private System.Windows.Forms.Button ConnectButton, SendButton, ComButton, QaButton;  // 接続ボタン，送信ボタン, 残りはドメインのボタン
        private System.Net.Sockets.Socket Sock; // サーバと通信するためのソケット
        private System.Windows.Forms.Label NowConnect;    //ラヴェル

        delegate void ReceiveBoxAddTextCallback(string text);
        // テキストボックス ReceiveBox の末尾に text を追加（別スレッドから呼び出し可）

        private void ReceiveBoxAddText(string text)
        {
            if (ReceiveBox.InvokeRequired)
                Invoke(new ReceiveBoxAddTextCallback(ReceiveBoxAddText), new object[] { text });
            else
                ReceiveBox.AppendText(text);
        }

        delegate void SwitchDisconnectedCallback();
        // 切断状態に移行（別スレッドから呼び出し可）
        private void SwitchDisconnected()
        {
            if (ConnectButton.InvokeRequired)
            {
                Invoke(new SwitchDisconnectedCallback(SwitchDisconnected));
            }
            else
            {
                ConnectButton.Enabled = true;       // 接続ボタンを有効化
                SendButton.Enabled = false;         // 送信ボタンを無効化
            }
            NowConnect.Text = "切断状態";
            NowConnect.BackColor = Color.LightPink;
        }

        // サーバから受信したデータ等を管理するためのクラス
        class AsyncStateObject
        {
            public System.Net.Sockets.Socket Sock;      // サーバと通信するためのソケット
            public byte[] ReceiveBuffer;                // サーバから受信したデータを一時的に格納するバッファ
            public System.IO.MemoryStream ReceivedData; // サーバから受信したデータを蓄積するメモリストリーム

            // コンストラクタ
            public AsyncStateObject(System.Net.Sockets.Socket sock)
            {
                Sock = sock;
                ReceiveBuffer = new byte[1024];
                ReceivedData = new System.IO.MemoryStream();
            }
        }

        // サーバからデータを受信したときに呼び出されるメソッド
        private void ReceiveDataCallback(System.IAsyncResult ar)
        {
            AsyncStateObject so = (AsyncStateObject)ar.AsyncState;  // サーバから受信したデータ等を管理するための状態オブジェクト
            int len = so.Sock.EndReceive(ar);     // 今回サーバから受信したデータのサイズ

            // サーバから切断されたときの処理
            if (len <= 0)
            {
                so.Sock.Close();
                SwitchDisconnected();
                return;
            }

            // 今回受信したデータをメモリストリームに蓄積
            so.ReceivedData.Write(so.ReceiveBuffer, 0, len);

            // データの受信が完了したときの処理
            if (so.Sock.Available == 0)
            {
                string str = System.Text.Encoding.UTF8.GetString(so.ReceivedData.ToArray()); // サーバから受信した文字列

                // テキストボックス内では改行を "\r\n" で表す必要があるため，"\n"→"\r\n" に置換
                str = str.Replace("\n", "\r\n");
                // テキストボックス ReceiveBox の末尾に受信した文字列を追加
                ReceiveBoxAddText(str);

                // メモリストリームに蓄積されたデータを消去
                so.ReceivedData.Close();
                so.ReceivedData = new System.IO.MemoryStream();
            }

            // サーバからのデータの待ち受けを再び開始
            so.Sock.BeginReceive(so.ReceiveBuffer, 0, so.ReceiveBuffer.Length,
                System.Net.Sockets.SocketFlags.None, new System.AsyncCallback(ReceiveDataCallback), so);
        }

        // サーバからのデータの待ち受けを開始（データ受信時に ReceiveDataCallback() が呼び出されるようにする）
        private void StartReceive(System.Net.Sockets.Socket sock)
        {
            // サーバから受信したデータ等を管理するための状態オブジェクトを生成
            AsyncStateObject so = new AsyncStateObject(sock);
            
            sock.BeginReceive(so.ReceiveBuffer, 0, so.ReceiveBuffer.Length,
                System.Net.Sockets.SocketFlags.None, new System.AsyncCallback(ReceiveDataCallback), so);
        }

        // 接続ボタンがクリックされたときに呼び出されるメソッド
        private void ConnectButton_Click(object sender, System.EventArgs e)
        {
            // サーバのホスト名またはIPアドレスを ServerBox.Text，ポート番号を PortBox.Text とする
            System.Net.IPAddress hostadd = System.Net.Dns.GetHostAddresses(ServerBox.Text)[0];
            System.Net.IPEndPoint ephost = new System.Net.IPEndPoint(hostadd, int.Parse(PortBox.Text));

            // サーバと通信するためのソケットを作成
            Sock = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);

            // サーバに接続
            Sock.Connect(ephost);

            // 接続状態に移行
            ConnectButton.Enabled = false;      // 接続ボタンを無効化
            SendButton.Enabled = true;          // 送信ボタンを有効化
            ReceiveBox.Text = "";               // テキストボックス ReceiveBox を初期化
            NowConnect.Text = "接続状態";       // 接続状態
            NowConnect.BackColor = Color.LightBlue;

            // サーバからのデータの待ち受けを開始
            StartReceive(Sock);
        }

        //.com
        private void ComButton_Click(object sender, System.EventArgs e)
        {
            ServerBox.Text += ".com";
            if(ServerBox.Text.Contains(".com") == true)
            {
                ComButton.Enabled = false;
                QaButton.Enabled = false;
            }
        }

        //.qa
        private void CnButton_Click(object sender, System.EventArgs e)
        {
            ServerBox.Text += ".jp";
            if (ServerBox.Text.Contains(".jp") == true)
            {
                ComButton.Enabled = false;
                QaButton.Enabled = false;
            }
        }

        // 送信ボタンがクリックされたときに呼び出されるメソッド
        private void SendButton_Click(object sender, System.EventArgs e)
        {
            // サーバに送信する文字列 SendBox.Text を byte 配列に変換して reqBytes に格納
            byte[] reqBytes = System.Text.Encoding.UTF8.GetBytes(SendBox.Text);

            // サーバにデータ reqBytes を送信
            Sock.Send(reqBytes, reqBytes.Length, System.Net.Sockets.SocketFlags.None);
        }

        public Program()
        {
            // ウィンドウに関する設定
            Text = "クライアントプログラム";
            ClientSize = new System.Drawing.Size(600, 400);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;

            // サーバのホスト名またはIPアドレスを入力するテキストボックスを配置
            ServerBox = new System.Windows.Forms.TextBox();
            ServerBox.Text = "www.google";
            ServerBox.Location = new System.Drawing.Point(20, 20);
            ServerBox.Size = new System.Drawing.Size(240, 20);
            Controls.Add(ServerBox);
            
            //.comをつけるボタン
            ComButton = new System.Windows.Forms.Button();
            ComButton.Text = ".com";
            ComButton.Location = new System.Drawing.Point(270, 20);
            ComButton.Size = new System.Drawing.Size(40, 20);
            ComButton.Click += new System.EventHandler(ComButton_Click);
            ComButton.Enabled = true;
            Controls.Add(ComButton);

            //.jpをつけるボタン
            QaButton = new System.Windows.Forms.Button();
            QaButton.Text = ".jp";
            QaButton.Location = new System.Drawing.Point(310, 20);
            QaButton.Size = new System.Drawing.Size(40, 20);
            QaButton.Click += new System.EventHandler(CnButton_Click);
            QaButton.Enabled = true;
            Controls.Add(QaButton);

            // サーバのポート番号を入力するテキストボックスを配置
            PortBox = new System.Windows.Forms.TextBox();
            PortBox.Text = "80";
            PortBox.Location = new System.Drawing.Point(360, 20);
            PortBox.Size = new System.Drawing.Size(40, 20);
            Controls.Add(PortBox);

            // 接続ボタンを配置
            ConnectButton = new System.Windows.Forms.Button();
            ConnectButton.Text = "接続";
            ConnectButton.Location = new System.Drawing.Point(410, 20);
            ConnectButton.Size = new System.Drawing.Size(40, 20);
            ConnectButton.Click += new System.EventHandler(ConnectButton_Click);
            Controls.Add(ConnectButton);

            //ラヴェルをつける初期化する
            NowConnect = new System.Windows.Forms.Label();
            NowConnect.Text = "切断状態";
            NowConnect.Location = new System.Drawing.Point(460, 20);
            NowConnect.Size = new System.Drawing.Size(55, 20);
            NowConnect.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            NowConnect.BackColor = Color.LightPink;
            Controls.Add(NowConnect);

            // サーバに送信する文字列を入力するテキストボックスを配置
            SendBox = new System.Windows.Forms.TextBox();
            SendBox.Text = "GET / HTTP/1.0\r\n\r\n";    // テキストボックス内では改行を "\r\n" で表す必要がある
            SendBox.Location = new System.Drawing.Point(20, 60);
            SendBox.Size = new System.Drawing.Size(500, 100);
            SendBox.Multiline = true;
            SendBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            Controls.Add(SendBox);

            // 送信ボタンを配置
            SendButton = new System.Windows.Forms.Button();
            SendButton.Text = "送信";
            SendButton.Location = new System.Drawing.Point(540, 60);
            SendButton.Size = new System.Drawing.Size(40, 20);
            SendButton.Click += new System.EventHandler(SendButton_Click);
            SendButton.Enabled = false;
            Controls.Add(SendButton);

            // サーバから受信した文字列を表示するテキストボックスを配置
            ReceiveBox = new System.Windows.Forms.TextBox();
            ReceiveBox.Location = new System.Drawing.Point(20, 180);
            ReceiveBox.Size = new System.Drawing.Size(560, 200);
            ReceiveBox.Multiline = true;
            ReceiveBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            Controls.Add(ReceiveBox);
        }

        static void Main(string[] args)
        {
            System.Windows.Forms.Application.Run(new Program());
        }
    }
}
