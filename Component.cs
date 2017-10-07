using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using LiveSplit.Model;
using LiveSplit.Options;
using usb2snes.core;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace LiveSplit.UI.Components
{
    public class USB2SNESComponent : LogicComponent
    {
        class Split
        {
            public string name { get; set; }
            public string address { get; set; }
            public string value { get; set; }
            public string type { get; set; }
            public List<Split> more { get; set; }

            public uint addressint { get { return Convert.ToUInt32(address, 16); } }
            public uint valueint { get { return Convert.ToUInt32(value, 16); } }
        }

        class Category
        {
            public string name { get; set; }
            public List<string> splits { get; set; }
        }

        class Game
        {
            public string name { get; set; }
            public Autostart autostart { get; set; }
            public List<Category> categories { get; set; }
            public List<Split> definitions { get; set; }
        }

        class Autostart
        {
            public string active { get; set; }
            public string address { get; set; }
            public string value { get; set; }
            public string type { get; set; }

            public uint addressint { get { return Convert.ToUInt32(address, 16); } }
            public uint valueint { get { return Convert.ToUInt32(value, 16); } }
        }

        public override string ComponentName => "USB2SNES Auto Splitter";
        private Timer _update_timer;
        private ComponentSettings _settings;
        private LiveSplitState _state;
        private TimerModel _model;
        private Game _game;
        private List<string> _splits;
        private bool _inTimer;
        private bool _error;


        public USB2SNESComponent(LiveSplitState state)
        {
            _state = state;
            _settings = new ComponentSettings();
            _model = new TimerModel() { CurrentState = _state };
            _state.RegisterTimerModel(_model);

            _splits = null;
            _inTimer = false;
            _error = false;

            _update_timer = new Timer() { Interval = 33 };
            _update_timer.Tick += (sender, args) => UpdateSplits();
            _update_timer.Enabled = true;

            _state.OnReset += _state_OnReset;
            _state.OnStart += _state_OnStart;
        }
        
        private bool checkSplits()
        {
            bool r = true;
            foreach(var c in _game.categories)
            {
                foreach (var s in c.splits)
                {
                    var d = _game.definitions.Where(x => x.name == s).FirstOrDefault();
                    if(d == null)
                    {
                        MessageBox.Show(String.Format("Split definition missing: {0}", s));
                        r = false;
                    }
                }
            }
            return r;
        }

        private bool connect()
        {
            if (!core.Connected())
            {
                try
                {
                    core.Connect(_settings.COMPort);
                }
                catch
                {
                    MessageBox.Show("Could not connect to sd2snes, check serial port settings.");
                    return false;
                }
            }

            return true;
        }

        private bool readConfig()
        {
            try
            {
                var jsonStr = File.ReadAllText(_settings.ConfigFile);
                _game = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Game>(jsonStr);
            }
            catch
            {
                MessageBox.Show("Could not open split config file, check config file settings.");
                return false;
            }
            if (!this.checkSplits())
            {
                MessageBox.Show("The split config file has missing definitions.");
                return false;
            }

            return true;
        }

        private void _state_OnStart(object sender, EventArgs e)
        {
            if(!core.Connected())
            {
                if(!this.connect())
                {
                    _model.Reset();
                    return;
                }
            }

            if(_game == null)
            {
                if(!this.readConfig())
                {
                    _model.Reset();
                    return;
                }
            }

            _error = false;

            _splits = _game.categories.Where(c => c.name.ToLower() == _state.Run.CategoryName.ToLower()).First()?.splits;
            if(_splits == null)
            {
                MessageBox.Show("There are no splits for the current category in the split config file, check that the run category is correctly set and exists in the config file.");
            }

        }

        private void _state_OnReset(object sender, TimerPhase value)
        {
            if (core.Connected())
            {
                if(_settings.ResetSNES)
                {
                    core.SendCommand(core.usbint_server_opcode_e.USBINT_SERVER_OPCODE_RESET, core.usbint_server_space_e.USBINT_SERVER_SPACE_SNES, core.usbint_server_flags_e.USBINT_SERVER_FLAGS_NONE, 0);
                }
            }
        }

        public override void Dispose()
        {
            _update_timer?.Dispose();
            if (core.Connected())
            {
                core.Disconnect();
            }
        }

        public override Control GetSettingsControl(LayoutMode mode)
        {
            return _settings;
        }

        public override XmlNode GetSettings(XmlDocument document)
        {
            return _settings.GetSettings(document);
        }

        public override void SetSettings(XmlNode settings)
        {
            _settings.SetSettings(settings);
        }

        public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height,
            LayoutMode mode)
        {
        }

        public void DoSplit()
        {
            if (_game.name == "Super Metroid" && core.Connected())
            {
                var data = new byte[512];
                core.SendCommand(core.usbint_server_opcode_e.USBINT_SERVER_OPCODE_GET, core.usbint_server_space_e.USBINT_SERVER_SPACE_SNES, core.usbint_server_flags_e.USBINT_SERVER_FLAGS_NONE, (uint)(0xF509DA), (uint)512);
                core.GetData(data, 0, 512);
                int ms = (data[0] + (data[1] << 8)) * (1000 / 60);
                int sec = data[2] + (data[3] << 8);
                int min = data[4] + (data[5] << 8);
                int hr = data[6] + (data[7] << 8);
                var gt = new TimeSpan(0, hr, min, sec, ms);
                _state.SetGameTime(gt);
                _model.Split();
            }
            else
            {
                _model.Split();
            }
        }

        private bool checkSplit(Split split, uint value, uint word)
        {
            bool ret = false;
            switch (split.type)
            {
                case "bit":
                    if ((value & split.valueint) != 0) { ret = true; }
                    break;
                case "eq":
                    if (value == split.valueint) { ret = true; }
                    break;
                case "gt":
                    if (value > split.valueint) { ret = true; }
                    break;
                case "lt":
                    if (value < split.valueint) { ret = true; }
                    break;
                case "gte":
                    if (value >= split.valueint) { ret = true; }
                    break;
                case "lte":
                    if (value <= split.valueint) { ret = true; }
                    break;
                case "wbit":
                    if ((word & split.valueint) != 0) { ret = true; }
                    break;
                case "weq":
                    if (word == split.valueint) { ret = true; }
                    break;
                case "wgt":
                    if (word > split.valueint) { ret = true; }
                    break;
                case "wlt":
                    if (word < split.valueint) { ret = true; }
                    break;
                case "wgte":
                    if (word >= split.valueint) { ret = true; }
                    break;
                case "wlte":
                    if (word <= split.valueint) { ret = true; }
                    break;
            }
            return ret;
        }

        public void UpdateSplits()
        {
            if (_inTimer == true)
                return;

            _inTimer = true;
            if (_state.CurrentPhase == TimerPhase.NotRunning)
            {
                if(_error == false && _settings.COMPort != null && _game == null && (!core.Connected()))
                {
                    if (!core.Connected())
                    {
                        if (!this.connect())
                        {
                            _error = true;
                            _inTimer = false;
                            return;
                        }
                    }

                    if (_game == null)
                    {
                        if (!this.readConfig())
                        {
                            _error = true;
                            _inTimer = false;
                            return;
                        }
                    }
                }

                if (_game != null && _game.autostart.active == "1")
                {
                    if (core.Connected())
                    {
                        var data = new byte[64];
                        core.SendCommand(core.usbint_server_opcode_e.USBINT_SERVER_OPCODE_GET, core.usbint_server_space_e.USBINT_SERVER_SPACE_SNES, core.usbint_server_flags_e.USBINT_SERVER_FLAGS_64BDATA, (0xF50000 + _game.autostart.addressint), (uint)64);
                        core.GetData(data, 0, 64);

                        uint value = (uint)data[0];
                        uint word = (uint)(data[0] + (data[1] << 8));

                        switch (_game.autostart.type)
                        {
                            case "bit":
                                if ((value & _game.autostart.valueint) != 0) { _model.Start(); }
                                break;
                            case "eq":
                                if (value == _game.autostart.valueint) { _model.Start(); }
                                break;
                            case "gt":
                                if (value > _game.autostart.valueint) { _model.Start(); }
                                break;
                            case "lt":
                                if (value < _game.autostart.valueint) { _model.Start(); }
                                break;
                            case "gte":
                                if (value >= _game.autostart.valueint) { _model.Start(); }
                                break;
                            case "lte":
                                if (value <= _game.autostart.valueint) { _model.Start(); }
                                break;
                            case "wbit":
                                if ((word & _game.autostart.valueint) != 0) { _model.Start(); }
                                break;
                            case "weq":
                                if (word == _game.autostart.valueint) { _model.Start(); }
                                break;
                            case "wgt":
                                if (word > _game.autostart.valueint) { _model.Start(); }
                                break;
                            case "wlt":
                                if (word < _game.autostart.valueint) { _model.Start(); }
                                break;
                            case "wgte":
                                if (word >= _game.autostart.valueint) { _model.Start(); }
                                break;
                            case "wlte":
                                if (word <= _game.autostart.valueint) { _model.Start(); }
                                break;
                        }
                    }
                }
            }
            else if (_state.CurrentPhase == TimerPhase.Running)
            {
                if (_splits != null)
                {
                    if (core.Connected())
                    {
                        var splitName = _splits[_state.CurrentSplitIndex];
                        var split = _game.definitions.Where(x => x.name == splitName).First();
                        var data = new byte[64];
                        core.SendCommand(core.usbint_server_opcode_e.USBINT_SERVER_OPCODE_GET, core.usbint_server_space_e.USBINT_SERVER_SPACE_SNES, core.usbint_server_flags_e.USBINT_SERVER_FLAGS_64BDATA, (0xF50000 + split.addressint), (uint)64);
                        core.GetData(data, 0, 64);

                        uint value = (uint)data[0];
                        uint word = (uint)(data[0] + (data[1] << 8));

                        bool ok = checkSplit(split, value, word);
                        if (split.more != null)
                        {
                            foreach (var moreSplit in split.more)
                            {
                                core.SendCommand(core.usbint_server_opcode_e.USBINT_SERVER_OPCODE_GET, core.usbint_server_space_e.USBINT_SERVER_SPACE_SNES, core.usbint_server_flags_e.USBINT_SERVER_FLAGS_64BDATA, (0xF50000 + moreSplit.addressint), (uint)64);
                                core.GetData(data, 0, 64);

                                value = (uint)data[0];
                                word = (uint)(data[0] + (data[1] << 8));

                                ok = ok && checkSplit(moreSplit, value, word);
                            }
                        }

                        if (ok)
                        {
                            DoSplit();
                        }
                    }
                }
            }
            _inTimer = false;
        }
    }
}
