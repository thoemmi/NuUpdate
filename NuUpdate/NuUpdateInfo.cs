namespace NuUpdate {
    internal class UpdateInstructions {
        public Shortcut[] Shortcuts;
    }

    internal class Shortcut {
        public string Title { get; set; }
        public string Description { get; set; }
        public string TargetPath { get; set; }
        public string Arguments { get; set; }
        public string IconPath { get; set; }
        public int IconIndex { get; set; }
    }
}