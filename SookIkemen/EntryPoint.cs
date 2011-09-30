using System;
using System.ComponentModel.Composition;
using System.Linq;
using Acuerdo.Plugin;
using Dulcet.Twitter;
using Inscribe.Common;
using Inscribe.Communication.Posting;
using Inscribe.Core;
using Inscribe.Storage;
using Inscribe.Subsystems;
using Inscribe.Storage.Perpetuation;

namespace SookIkemen
{
    [Export(typeof(IPlugin))]
    public class EntryPoint : IPlugin
    {
        public string Name
        {
            get { return "スークイケメンﾅｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰ"; }
        }

        public double Version
        {
            get { return 1.1; }
        }

        public void Loaded()
        {
            KeyAssignCore.RegisterOperation("SookIkemen", () =>
                KeyAssignHelper.ExecuteTabAction(tab =>
                {
                    try
                    {
                        tab.TabProperty.LinkAccountInfos.ForEach(a =>
                            PostOffice.UpdateTweet(a, "スークイケメンﾅｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰｰ #sook_ikemen"));
                    }
                    catch (Exception e)
                    {
                        ExceptionStorage.Register(e, ExceptionCategory.PluginError, "スークイケメンﾅｰｰｰｰｰｰｰｰｰｰｰｰｰｰに失敗しました: " + e.Message);
                    }
                }));
            KeyAssignCore.RegisterOperation("SenselessRetweet", () =>
                KeyAssignHelper.ExecuteTVMAction(tvm =>
                    {
                        if (tvm.Tweet.Backend.IsDirectMessage) return;
                        KernelService.MainWindowViewModel.InputBlockViewModel.SetOpenText(true, true);
                        KernelService.MainWindowViewModel.InputBlockViewModel.SetText(BuildSenseless(tvm.Tweet.Backend));
                    }));
            KeyAssignCore.RegisterOperation("SenselessRetweetFast", () =>
                KeyAssignHelper.ExecuteTVMAction(tvm =>
                {
                    try
                    {
                        if (tvm.Tweet.Backend.IsDirectMessage) return;
                        tvm.Parent.TabProperty.LinkAccountInfos.ForEach(
                            ai => PostOffice.UpdateTweet(ai, BuildSenseless(tvm.Tweet.Backend)));
                    }
                    catch (Exception e)
                    {
                        ExceptionStorage.Register(e, ExceptionCategory.PluginError, "非常識RTに失敗しました: " + e.Message);
                    }
                }));
        }

        private string BuildSenseless(TweetBackend tb)
        {
            var rtos = tb.RetweetedOriginalId != 0 ? TweetStorage.Get(tb.RetweetedOriginalId) : null;
            if (rtos != null)
                tb = rtos.Backend;
            var user = UserStorage.Lookup(tb.UserId);
            return "… RT @" + user.Backend.ScreenName + ": " + rtos.Backend.Text;
        }

        public IConfigurator ConfigurationInterface
        {
            get { return null; }
        }
    }
}
