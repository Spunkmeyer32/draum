using DRaumServerApp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Telegram.Bot.Types;

namespace DRaumServerTest
{
  [TestClass]
  public class SpamFilterTest
  {
    [TestMethod]
    public void spamFilterTest()
    {
      string input = "& < > \" und <tag>";
      Assert.IsFalse(SpamFilter.checkPostInput(input, out string output, out string message));
      for (int i = 0; i < 100; i++)
      {
        input += "X";
      }
      input = Utilities.telegramEntitiesToHtml(input, new MessageEntity[0]);
      Assert.IsTrue(SpamFilter.checkPostInput(input, out  output, out  message));
      Assert.IsTrue(output.StartsWith("&amp; &lt; &gt; &quot; und &lt;tag&gt;"));
    }
    
  }
}