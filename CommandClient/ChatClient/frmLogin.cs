using System;
using System.Windows.Forms;
using System.Net;
using Proshot.CommandClient;
using Proshot.UtilityLib.CommonDialogs;

namespace ChatClient
{
	public partial class frmLogin : Form
	{
		private bool _canClose;
		public CMDClient Client { get; private set; }

		public frmLogin(IPAddress serverIp, int serverPort)
		{
			InitializeComponent();
			_canClose = false;
			CheckForIllegalCrossThreadCalls = false;
			Client = new CMDClient(serverIp, serverPort, "None");
			Client.CommandReceived += CommandReceived;
			Client.ConnectingSuccessed += client_ConnectingSuccessed;
			Client.ConnectingFailed += client_ConnectingFailed;
		}

		private void client_ConnectingFailed(object sender, EventArgs e)
		{
			var popup = new frmPopup(PopupSkins.SmallInfoSkin);
			popup.ShowPopup("Error", "Server Is Not Accessible !", 200, 2000, 2000);
			SetEnablity(true);
		}

		private void client_ConnectingSuccessed(object sender, EventArgs e)
		{
			Client.SendCommand(new Command(CommandType.IsNameExists, Client.Ip, Client.NetworkName));
		}

		void CommandReceived(object sender, CommandEventArgs e)
		{
			if (e.Command.CommandType == CommandType.IsNameExists)
			{
				if (e.Command.MetaData.ToLower() == "true")
				{
					var popup = new frmPopup(PopupSkins.SmallInfoSkin);
					popup.ShowPopup("Error", "The Username is already exists !", 300, 2000, 2000);
					Client.Disconnect();
					SetEnablity(true);
				}
				else
				{
					_canClose = true;
					Close();
				}
			}
		}

		private void LoginToServer()
		{
			if (txtUsetName.Text.Trim() == "")
			{
				var popup = new frmPopup(PopupSkins.SmallInfoSkin);
				popup.ShowPopup("Error", "Username is empty !", 1000, 2000, 2000);
				SetEnablity(true);
			}
			else
			{
				Client.NetworkName = txtUsetName.Text.Trim();
				Client.ConnectToServer();
			}
		}

		private void btnEnter_Click(object sender, EventArgs e)
		{
			SetEnablity(false);
			LoginToServer();
		}

		private void SetEnablity(bool enable)
		{
			btnEnter.Enabled = enable;
			txtUsetName.Enabled = enable;
			btnExit.Enabled = enable;
		}

		private void btnExit_Click(object sender, EventArgs e)
		{
			_canClose = true;
		}

		private void frmLogin_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!_canClose)
			{
				e.Cancel = true;
			}
			else
			{
				Client.CommandReceived -= CommandReceived;
			}
		}
	}
}