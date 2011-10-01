
namespace Inscribe.Configuration.Settings
{
    public class KernelProperty
    {
        public int ImageGCInitialDelay = 1000 * 60 * 3;

        public int ImageGCInterval = 1000 * 60;

        public int ImageLifetime = 1000 * 60 * 10;

        public int TweetCacheMaxCount = 10000;

        public double TweetCacheSurviveDensity = 0.5;

        public int UserCacheMaxCount = 1000;

        public double UserCacheSurviveDensity = 0.5;

        public int ImageCacheMaxCount = 1000;

        public double ImageCacheSurviveDensity = 0.5;
    }
}
