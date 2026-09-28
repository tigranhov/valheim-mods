using System;

namespace Cartographer
{
    /// <summary>Asks for a line of text with the game's own sign dialog.</summary>
    internal sealed class TextPrompt : TextReceiver
    {
        private readonly Action<string> _done;
        private readonly string _start;

        private TextPrompt(string start, Action<string> done)
        {
            _start = start;
            _done = done;
        }

        /// <summary>True while a prompt we opened is still on screen.</summary>
        public static bool Open => TextInput.instance != null && TextInput.instance.m_panel != null && TextInput.instance.m_panel.activeSelf;

        public static void Ask(string topic, string start, int limit, Action<string> done)
        {
            if (TextInput.instance == null)
            {
                return;
            }
            TextInput.instance.RequestText(new TextPrompt(start, done), topic, limit);
        }

        public string GetText()
        {
            return _start ?? "";
        }

        public void SetText(string text)
        {
            _done(text?.Trim() ?? "");
        }
    }
}
