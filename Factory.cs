using System;
using LiveSplit.Model;
using LiveSplit.UI.Components;

[assembly: ComponentFactory(typeof(Factory))]

namespace LiveSplit.UI.Components
{
    public class Factory : IComponentFactory
    {
        public string ComponentName => "USB2SNES Auto Splitter";
        public string Description => "Uses the SD2SNES USB2SNES firmware to monitor RAM for auto splitting.";
        public ComponentCategory Category => ComponentCategory.Control;
        public Version Version => Version.Parse("1.0.0");

        public string UpdateName => ComponentName;
        public string UpdateURL => "";
        public string XMLURL => "";

        public IComponent Create(LiveSplitState state) => new USB2SNESComponent(state);
    }
}
