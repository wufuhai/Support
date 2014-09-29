using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AxMSTSCLib;
using MSTSCLib;

namespace ProcessAsUser {
    public partial class RDClient : Form {
        private readonly ProcessAsUser _processAsUser;

        public RDClient() {
            InitializeComponent();
        }

        public RDClient(ProcessAsUser processAsUser):this(){
            _processAsUser = processAsUser;
            Load += OnLoad;
            rdp.OnLoginComplete+=RdpOnOnLoginComplete;
        }

        private void RdpOnOnLoginComplete(object sender, EventArgs eventArgs){
            Trace.TraceInformation("LoginComplete");
            Task task = Task.Factory.StartNew(() =>{
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                while (!_processAsUser.SessionExists()){
                    Thread.Sleep(1000);
                    if (stopwatch.ElapsedMilliseconds < 5000)
                        break;
                }
            });
            Task.WaitAll(task);
            _processAsUser.CreateProcess();
            Close();
        }

        private void OnLoad(object sender, EventArgs eventArgs){
            bool sessionExists = _processAsUser.SessionExists();
            Trace.TraceInformation("SessionExists=" + sessionExists);
            if (!sessionExists)
                Connect(_processAsUser.Options.UserName, _processAsUser.Options.Password);
            else{
                _processAsUser.CreateProcess();
                Close();
            }
        }

        public AxMsTscAxNotSafeForScripting Rdp {
            get { return rdp; }
        }

        public void Connect(string userName, string password) {
            rdp.DesktopWidth = 1440;
            rdp.DesktopHeight = 900;
            rdp.Server = Environment.MachineName;
            rdp.UserName = userName;
            var secured = (IMsTscNonScriptable)rdp.GetOcx();
            secured.ClearTextPassword = password;
            rdp.Connect();
        }

        private void button1_Click(object sender, EventArgs e) {
            try {
                Connect(txtUserName.Text, txtPassword.Text);
            }
            catch (Exception ex) {
                MessageBox.Show("Error Connecting",
                    "Error connecting to remote desktop " + txtServer.Text + " Error:  " + ex.Message,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button2_Click(object sender, EventArgs e) {
            try {
                if (rdp.Connected.ToString(CultureInfo.InvariantCulture) == "1")
                    rdp.Disconnect();
            }
            catch (Exception ex) {
                MessageBox.Show("Error Disconnecting",
                    "Error disconnecting from remote desktop " + txtServer.Text + " Error:  " + ex.Message,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}