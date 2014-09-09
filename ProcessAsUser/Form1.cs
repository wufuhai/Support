using System;
using System.Globalization;
using System.Windows.Forms;
using AxMSTSCLib;
using MSTSCLib;

namespace ProcessAsUser{
    public partial class Form1 : Form{
        public Form1(){
            InitializeComponent();
        }

        public Form1(string userName, string password){
            Visible = false;
            InitializeComponent();
            rdp.OnConnected += rdp_OnConnected;
            rdp.OnFatalError += rdp_OnFatalError;
            rdp.OnLogonError += rdp_OnLogonError;
            Connect(userName, password);
        }

        private static bool _connected;

        public static bool Connected{
            get { return _connected; }
        }

        private void rdp_OnLogonError(object sender, IMsTscAxEvents_OnLogonErrorEvent e){
            Close();
        }

        private void rdp_OnFatalError(object sender, IMsTscAxEvents_OnFatalErrorEvent e){
            Close();
        }

        private void rdp_OnConnected(object sender, EventArgs e){
            _connected = true;
            Close();
        }


        public AxMsTscAxNotSafeForScripting Rdp{
            get { return rdp; }
        }

        public void Connect(string userName, string password){
            rdp.Server = Environment.MachineName;
            rdp.UserName = userName;
            var secured = (IMsTscNonScriptable)rdp.GetOcx();
            secured.ClearTextPassword = password;
            rdp.Connect();
        }

        private void button1_Click(object sender, EventArgs e){
            try{
                Connect(txtUserName.Text, txtPassword.Text);
            }
            catch (Exception ex){
                MessageBox.Show("Error Connecting",
                    "Error connecting to remote desktop " + txtServer.Text + " Error:  " + ex.Message,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button2_Click(object sender, EventArgs e){
            try{
                if (rdp.Connected.ToString(CultureInfo.InvariantCulture) == "1")
                    rdp.Disconnect();
            }
            catch (Exception ex){
                MessageBox.Show("Error Disconnecting",
                    "Error disconnecting from remote desktop " + txtServer.Text + " Error:  " + ex.Message,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Form1_Load(object sender, EventArgs e){
            Visible = false;
        }
    }
}