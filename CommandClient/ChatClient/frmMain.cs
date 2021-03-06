using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using System.Net;
using Proshot.CommandClient;
using Timer = System.Timers.Timer;

namespace ChatClient
{
	public partial class frmMain : Form
	{
		private static string _serverIp = "";
		private CMDClient _client;
		private readonly List<frmPrivate> _privateWindowsList;
		
		public frmMain()
		{
			InitializeComponent();
			_privateWindowsList = new List<frmPrivate>();
			SetServerIp();

			StartListening();
			UpdateMarks();
		}

		private void BlockEdit()
		{
			mniEnter.Enabled = _hasServerIp;
		}

		private TcpClient _newClient;
		private bool _hasServerIp;

		private void StartListening()
		{
			if (_hasServerIp)
			{
				_newClient = new TcpClient();
				try
				{
					_client = new CMDClient(IPAddress.Parse(_serverIp), 8001, "None");
					_newClient.Connect(_serverIp, 8002);
					var readingThread = new Thread(StartReading);
					readingThread.Start();
					_hasServerIp = true;
				}
				catch
				{
					_hasServerIp = false;
				}
			}
			BlockEdit();
		}

		private void StartReading()
		{
			while (true)
			{
				var stream = _newClient.GetStream();
				var formatter = new BinaryFormatter();

				var img = (Image)formatter.Deserialize(stream);
				pictureBox1.BackgroundImage = img;

				splitContainer.Enabled = true;
			}
		}

		private void SetServerIp(object sender, EventArgs e)
		{
			SetServerIp();
			StartListening();
		}

		private void SetServerIp()
		{
			var serverForm = new GetServerIp {StartPosition = FormStartPosition.CenterScreen};
			serverForm.ShowDialog();
			if (serverForm.IsCancel)
			{
				_hasServerIp = !serverForm.IsCancel;
				Messages.Text = firstMessage;
				return;
			}
			_serverIp = serverForm.ServerIp;
			_hasServerIp = !string.IsNullOrEmpty(_serverIp);
		}

		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (keyData == Keys.Enter)
			{
				//SendMessage();
			}
			return base.ProcessCmdKey(ref msg, keyData);
		}

		private bool IsPrivateWindowOpened(string remoteName)
		{
			foreach (var privateWindow in _privateWindowsList)
			{
				if (privateWindow.RemoteName == remoteName)
				{
					return true;
				}
			}
			return false;
		}

		private frmPrivate FindPrivateWindow(string remoteName)
		{
			foreach (var privateWindow in _privateWindowsList)
			{
				if (privateWindow.RemoteName == remoteName)
				{
					return privateWindow;
				}
			}
			return null;
		}

		List<int> _shots = new List<int>();
		Timer _timer = new Timer(2000);

		private void OnTimedEvent(object source, ElapsedEventArgs e)
		{
			Sound.PlaySound(_shots[_shots.Count - 1]);
			//txtMessages.Text = _shotMessage;
			_timer.Stop();
			//if (_shots.Count >= _shotNumber)
			//{
			//   cancelServer(new object(), new EventArgs());
			//}
		}

		private void OnTimerMarkEvent(object source, ElapsedEventArgs e)
		{
			Sound.PlaySound(_fullMark);
			_timerMark.Stop();
		}

		private Timer _timerMark = new Timer(1500);

		private int _fullMark = 0;

		private string _clientName = "";

		void client_CommandReceived(object sender, CommandEventArgs e)
		{
			_clientName = e.Command.SenderName;
			switch (e.Command.CommandType)
			{
				case (CommandType.Message):
					if (e.Command.Target.Equals(IPAddress.Broadcast))
					{
						var points = e.Command.MetaData.Split(' ');
						bool boolen;
						var isPoint = bool.TryParse(points[0], out boolen);

						if (isPoint)
						{
							switch (points.Length)
							{
								case 3:
									var x = int.Parse(points[1]);
									var y = int.Parse(points[2]);

									pictureBox1.CreateGraphics().FillEllipse(Brushes.Red, x - 7/2, y - 7/2, 6, 6);
									pictureBox1.CreateGraphics().DrawEllipse(new Pen(Brushes.Black, 1), x - 7/2, y - 7/2, 7, 7);

									break;
								case 2:
									if ((_shotTime - DateTime.Now).TotalSeconds <= 3)
									{
										txtMessages.Text += e.Command.SenderName + ": " + e.Command.MetaData.Split(' ')[1] + Environment.NewLine;
										
										int shot;
										var isShot = int.TryParse(points[1], out shot);
										if (isShot)
										{
											_shotOnServer = true;
											_shots.RemoveAt(_shots.Count - 1);
											_shots.Add(shot);
											_timer.Elapsed += OnTimedEvent;
											_timer.Interval = 1000;
											_timer.Start();
										}
									}
									break;
							}
						}
						else
						{
							Sound.Play("mark");
							var hasPoint = int.TryParse(points[1], out _fullMark);
							_timerMark.Interval = 1500;
							_timerMark.Elapsed += OnTimerMarkEvent;
							_timerMark.Start();
							txtMessages.Text += "-----------------------" + Environment.NewLine + string.Format("Оценка: {0}", _fullMark) + Environment.NewLine;
						}
					}

					else if (!IsPrivateWindowOpened(e.Command.SenderName))
					{
						OpenPrivateWindow(e.Command.SenderIp, e.Command.SenderName, e.Command.MetaData);
						ShareUtils.PlaySound(ShareUtils.SoundType.NewMessageWithPow);
					}
					break;

				case (CommandType.FreeCommand):
					var newInfo = e.Command.MetaData.Split(new[] {':'});
					AddToList(newInfo[0], newInfo[1]);
					ShareUtils.PlaySound(ShareUtils.SoundType.NewClientEntered);
					break;
				case (CommandType.SendClientList):
					var clientInfo = e.Command.MetaData.Split(new[] {':'});
					AddToList(clientInfo[0], clientInfo[1]);
					break;
				case (CommandType.ClientLogOffInform):
					RemoveFromList(e.Command.SenderName);
					break;
			}
		}

		private void RemoveFromList(string name)
		{
			var item = lstViwUsers.FindItemWithText(name);
			if (item.Text != _client.Ip.ToString())
			{
				lstViwUsers.Items.Remove(item);
				ShareUtils.PlaySound(ShareUtils.SoundType.ClientExit);
			}

			var target = FindPrivateWindow(name);
			if (target != null)
			{
				target.Close();
			}
		}

		private const string firstMessage = "Подключение не выполнено. Повторите подключение выбрав в пункте меню \"Аутентификая\" раздел \"ВВЕСТИ IP СЕРВЕРА\"";
		private const string secondMessage = "Введите имя для авторизации в программе и нажмите кнопку \"Ввести\"";
		private const string thirdMessage = "Вход не выполнен. Повторите вход выбрав в пункте меню \"Аутентификая\" раздел \"ВХОД\"";
		private const string fourthMessage = "На компьютере \"СЕРВЕРЕ\" подключите камеру, создайте изображение. ПРИСТУПИТЕ К СТРЕЛЬБЕ";
		private const string fifthMessage = "Выполните выстрелы";
		private const string sixthMessage = "Нажмите кнопку \"СТАРТ\" для начала стрельбы";	

		private void mniEnter_Click(object sender, EventArgs e)
		{
			if (mniEnter.Text == "Вход")
			{
				if (!string.IsNullOrEmpty(_serverIp))
				{
					Messages.Text = secondMessage;
					var dlg = new frmLogin(IPAddress.Parse(_serverIp), 8001);
					dlg.ShowDialog();
					_client = dlg.Client;
					Messages.Text = thirdMessage;
					if (_client.Connected)
					{
						_client.CommandReceived += client_CommandReceived;
						_client.SendCommand(new Command(CommandType.FreeCommand, IPAddress.Broadcast,
						                                _client.Ip + ":" + _client.NetworkName));
						_client.SendCommand(new Command(CommandType.SendClientList, _client.ServerIp));
						AddToList(_client.Ip.ToString(), _client.NetworkName);
						mniEnter.Text = "Выход";
						mniEnter.Enabled = splitContainer.Enabled = false;
						Messages.Text = fourthMessage;
					}
				}
			}
			else
			{
				mniEnter.Text = "Вход";
				_privateWindowsList.Clear();
				_client.Disconnect();
				lstViwUsers.Items.Clear();
				mniEnter.Enabled = splitContainer.Enabled = true;
			}
		}

		//private void EnterLogin()
		//{
		//   if (mniEnter.Text == "Вход")
		//   {
		//      var dlg = new frmLogin(IPAddress.Parse(_serverIp), 8001);
		//      dlg.ShowDialog();
		//      _client = dlg.Client;

		//      if (_client.Connected)
		//      {
		//         _client.CommandReceived += client_CommandReceived;
		//         _client.SendCommand(new Command(CommandType.FreeCommand, IPAddress.Broadcast, _client.Ip + ":" + _client.NetworkName));
		//         _client.SendCommand(new Command(CommandType.SendClientList, _client.ServerIp));
		//         AddToList(_client.Ip.ToString(), _client.NetworkName);
		//         mniEnter.Text = "Выход";
		//         mniEnter.Enabled = splitContainer.Enabled = false;
		//      }
		//   }
		//   else
		//   {
		//      mniEnter.Text = "Вход";
		//      _privateWindowsList.Clear();
		//      _client.Disconnect();
		//      lstViwUsers.Items.Clear();
		//      mniEnter.Enabled = splitContainer.Enabled = true;
		//   }
		//}


		private void mniExit_Click(object sender , EventArgs e)
		{
			Close();
		}

		private void SendMessage(string message)
		{
			if (_client.Connected)
			{
				_client.SendCommand(new Command(CommandType.Message, IPAddress.Broadcast, message/*txtNewMessage.Text*/));
			}
		}

		private void AddToList(string ip,string name)
		{
			var newItem = lstViwUsers.Items.Add(ip);
			newItem.ImageKey = "Smiely.png";
			newItem.SubItems.Add(name);
		}

		private void OpenPrivateWindow(IPAddress remoteClientIp,string clientName)
		{
			if ( _client.Connected )
			{
				if ( !IsPrivateWindowOpened(clientName) )
				{
					var privateWindow = new frmPrivate(_client , remoteClientIp , clientName);
					_privateWindowsList.Add(privateWindow);
					privateWindow.FormClosed += privateWindow_FormClosed;
					privateWindow.StartPosition = FormStartPosition.CenterParent;
					privateWindow.Show(this);
				}
			}
		}

		private void OpenPrivateWindow(IPAddress remoteClientIp , string clientName , string initialMessage)
		{
			if (_client.Connected )
			{
				var privateWindow = new frmPrivate(_client , remoteClientIp , clientName , initialMessage);
				_privateWindowsList.Add(privateWindow);
				privateWindow.FormClosed += privateWindow_FormClosed;
				privateWindow.Show(this);
			}
		}

		void privateWindow_FormClosed(object sender , FormClosedEventArgs e)
		{
			this._privateWindowsList.Remove((frmPrivate)sender);
		}

		private void btnPrivate_Click(object sender , EventArgs e)
		{
			StartPrivateChat();
		}

		private void StartPrivateChat()
		{
			if (lstViwUsers.SelectedItems.Count != 0)
			{
				OpenPrivateWindow(IPAddress.Parse(lstViwUsers.SelectedItems[0].Text),
				lstViwUsers.SelectedItems[0].SubItems[1].Text);
			}
		}

		private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
		{
			Proshot.LanguageManager.LanguageActions.ChangeLanguageToEnglish();
			try
			{
				_client.Disconnect();
			}
			catch
			{

			}
		}

		private void startServer(object sender, EventArgs e)
		{
			StartEvent.Enabled = false;
			CancelEvent.Enabled = true;
			SendMessage("start");
			txtMessages.Text = string.Empty;
			pictureBox1.Image = null;
			pictureBox1.Invalidate();

			_shots.Clear();

			StartServer();
		}

		private void StartServer()
		{
			Sound.Play("startShot");
			Messages.Text = fifthMessage;
		}

		private void cancelServer(object sender, EventArgs e)
		{
			CancelServer();
		}

		private void CancelServer()
		{
			Messages.Text = sixthMessage;
			StartEvent.Enabled = true;
			CancelEvent.Enabled = false;
			SendMessage("cancel");
			Sound.Play("owerShot");
			RezaltWrite(_shots);
		}

		private bool _shotIsMade;
		private DateTime _shotTime = DateTime.Now;

		private bool _shotOnServer;

		private readonly Timer _newTimer = new Timer(3000);

		private void MakeShot(object sender, MouseEventArgs e)
		{
			//if (e.Button == MouseButtons.Middle && splitContainer.Enabled)
			//{
			if (!StartEvent.Enabled && splitContainer.Enabled && e.Button == MouseButtons.Middle)
			{
				Sound.Play("shot");

				_shotIsMade = true;
				_shotTime = DateTime.Now;
				_shots.Add(0);

				_newTimer.Interval = 3000;
				_newTimer.Elapsed += OnShotEvent;
				_newTimer.Start();
			}
			//}
		}

		private void OnShotEvent(object sender, ElapsedEventArgs e)
		{
			if (_shotIsMade)
			{
				if (!_shotOnServer)
				{
					var currentShot = _shots[_shots.Count - 1];
					Sound.PlaySound(currentShot);
					txtMessages.Text += _clientName + ": " + currentShot + Environment.NewLine;
					//txtMessages.Text = "";
					// = _shotMessage;
				}

				_shotOnServer = false;

				if (_shots.Count >= _shotNumber)
				{
					_timerLastShot = new Timer();
					_timerLastShot.Interval = 1000;
					_timerLastShot.Elapsed += TimerLastShotElapsed;
					_timerLastShot.Start();

				}
			}
			_shotIsMade = false;
			_newTimer.Stop();
		}

		void TimerLastShotElapsed(object sender, ElapsedEventArgs e)
		{
			CancelServer();
			_timerLastShot.Stop();
		}

		private Timer _timerLastShot = new Timer();

		int _shotNumber;
		int _on5;
		int _on4;
		int _on3;
		int _on2;

		public void UpdateMarks()
		{
			try
			{
				var str = new StreamReader("C:\\Program Files\\LazerShot\\pass.tir");
				var s = str.ReadToEnd();
				var sq = s.ToCharArray();
				_shotNumber = sq[0];
				_on5 = sq[2];
				_on4 = sq[4];
				_on3 = sq[6];
				_on2 = sq[8];
				str.Close();
			}
			catch
			{
				if (File.Exists("C:\\Program Files\\LazerShot\\pass.tir"))
					File.Delete("C:\\Program Files\\LazerShot\\pass.tir");

				if (!Directory.Exists("C:\\Program Files\\LazerShot"))
				{
					Directory.CreateDirectory("C:\\Program Files\\LazerShot");
				}

				const string pass = "....";
				var str1 = new StreamWriter("C:\\Program Files\\LazerShot\\pass.tir");
				str1.Write(pass);
				str1.Close();
				UpdateMarks();
			}
		}

		private void RezaltWrite(IEnumerable<int> rezalt)
		{
			try
			{
					var symmaryRez = 0;
					foreach (var result in rezalt)
					{
						symmaryRez += result;
					}

					txtMessages.Text += Environment.NewLine + ("---------------------");

					txtMessages.Text += Environment.NewLine + string.Format("Сума: < {0} > очок", symmaryRez) + Environment.NewLine;
					//if ((rezalt.Count == _shotNumber))
					//{
					FinalityShot(symmaryRez);
					//}
			}
			catch
			{
				RezaltWrite(rezalt);
			}
		}

		private int _mark = 1;

		private void FinalityShot(int symmaryRez)
		{
			if (symmaryRez >= _on5)
			{
				txtMessages.Text += ("Оцінка - 5");
				_mark = 5;
			}
			else
			{
				if (symmaryRez >= _on4)
				{
					txtMessages.Text += ("Оцінка - 4");
					_mark = 4;
				}
				else
				{
					if (symmaryRez >= _on3)
					{
						txtMessages.Text += ("Оцінка - 3");
						_mark = 3;
					}
					else
					{
						if (symmaryRez >= _on2)
						{
							txtMessages.Text += ("Оцінка - 2");
							_mark = 2;
						}
						else
						{
							txtMessages.Text += ("Оцінка менше 2");
							_mark = 1;
						}
					}
				}
			}
			_resultTimer.Interval = 1500;
			_resultTimer.Elapsed += ResultTimer;
			_resultTimer.Start();
			//startStop = false;
			//Stop.Enabled = false;
			//Start.Enabled = true;
		}

		private void ResultTimer(object sender, ElapsedEventArgs e)
		{
			Sound.Play("mark");
			_resultTimer.Stop();
			
			_timerResultMark.Interval = 1500;
			_timerResultMark.Elapsed += TimerResultMarkEvent;
			_timerResultMark.Start();
		}

		private void TimerResultMarkEvent(object sender, ElapsedEventArgs e)
		{
			_timerResultMark.Stop();
			Sound.PlaySound(_mark);
		}

		private Timer _timerResultMark = new Timer();

		private Timer _resultTimer = new Timer();
	}
}