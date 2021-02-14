using System.Collections.Generic;
using Telegram.Bot.Types.ReplyMarkups;

namespace DRaumServerApp.telegram
{
  internal class Keyboards
  {
    private static InlineKeyboardMarkup _getNextPostToModerateKeyboard;

    internal static InlineKeyboardMarkup getGetNextPostToModerateKeyboard()
    {
      if (_getNextPostToModerateKeyboard == null)
      {
        InlineKeyboardButton getNextModPostButton = InlineKeyboardButton.WithCallbackData("Beitrag laden", DRaumManager.modGetNextCheckPostPrefix + "0");
        _getNextPostToModerateKeyboard = new InlineKeyboardMarkup(new List<InlineKeyboardButton>
        {
          getNextModPostButton
        });
      }
      return _getNextPostToModerateKeyboard;
    }

    internal static InlineKeyboardMarkup getTopPostLinkKeyboard(long messageId, string roomname)
    {
      InlineKeyboardButton linkbutton = InlineKeyboardButton.WithUrl("Lesen und Abstimmen", "https://t.me/" + roomname + "/" + messageId);
      List<InlineKeyboardButton> buttonlist = new List<InlineKeyboardButton>
      {
        linkbutton
      };
      return new InlineKeyboardMarkup(buttonlist);
    }

    internal static InlineKeyboardMarkup getPostKeyboard(int upvotePercentage, long postId)
    {
      int downvotePercentage = 100 - upvotePercentage;
      InlineKeyboardButton thumbsUpButton = InlineKeyboardButton.WithCallbackData("👍 " + upvotePercentage + "%", DRaumManager.voteUpPrefix + postId);
      InlineKeyboardButton thumbsDownButton = InlineKeyboardButton.WithCallbackData("👎 " + downvotePercentage + "%", DRaumManager.voteDownPrefix + postId);
      InlineKeyboardButton flagButton = InlineKeyboardButton.WithCallbackData("🚩 Melden", DRaumManager.flagPrefix + postId);
      List<InlineKeyboardButton> buttonlist = new List<InlineKeyboardButton>
      {
        thumbsUpButton,
        thumbsDownButton,
        flagButton
      };
      return new InlineKeyboardMarkup(buttonlist);
    }


  }
}