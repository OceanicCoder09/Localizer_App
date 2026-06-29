using System;

namespace Localizer_App.Services
{
    // Custom exception for terminal API errors (e.g. invalid API key)
    public class FatalGeminiException : Exception
    {
        public FatalGeminiException(string message) : base(message) { }
    }
}
