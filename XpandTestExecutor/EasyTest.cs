using System.Collections.Generic;

namespace XpandTestExecutor{
    public class EasyTest {
        private readonly int _winPort;
        private readonly int _webPort;
        private readonly List<User> _users = new List<User>();

        public EasyTest(int winPort, int webPort) {
            _winPort = winPort;
            _webPort = webPort;
        }

        public int WinPort {
            get { return _winPort; }
        }

        public List<User> Users {
            get { return _users; }
        }

        public int WebPort {
            get { return _webPort; }
        }

        public string FileName { get; set; }

        public string Index { get; set; }


    }
}