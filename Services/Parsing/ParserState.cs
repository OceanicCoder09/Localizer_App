namespace Localizer_App.Services
{
    // Tracks state indicators for block scoping (STRINGTABLE context tracking)
    internal class ParserState
    {
        public bool InStringTable { get; set; }
        public int NestingLevel { get; set; }
        public Token? LastKeyToken { get; set; }
    }
}
