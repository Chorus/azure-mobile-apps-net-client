using Microsoft.WindowsAzure.MobileServices;

namespace SQLiteStore.Tests
{
    public class MobileServiceNetDateTimeConverter : MobileServiceIsoDateTimeConverter
    {
        public MobileServiceNetDateTimeConverter()
        {
            DateTimeFormat = "yyyy'-'MM'-'dd' 'HH':'mm':'ss'.'FFFFFFF";
        }
    }
}
