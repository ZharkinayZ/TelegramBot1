namespace TelegramBot
{
    internal class UserState
    {
        public Dictionary<string, int> Products { get; set; }
        public Dictionary<string, int> CashProducts { get; set; }
        public List<Dictionary<string, int>> HistoryProducts { get; set; }
        public string FullName { get; set; }
    }
}
