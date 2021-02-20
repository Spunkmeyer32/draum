using System.Collections.Generic;
using Telegram.Bot.Types.ReplyMarkups;

namespace DRaumServerApp.telegram
{
  internal class Keyboards
  {
    private static InlineKeyboardMarkup _getNextPostToModerateKeyboard;

    /// <summary>
    /// [ Beitrag laden ]
    /// </summary>
    /// <returns></returns>
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

    /// <summary>
    /// [ Lesen und Abstimmen -> ]
    /// </summary>
    /// <param name="messageId">Msg-ID des Posts im Draum</param>
    /// <param name="roomname">Chat-Name des Draums für den Link</param>
    /// <returns></returns>
    internal static InlineKeyboardMarkup getTopPostLinkKeyboard(long messageId, string roomname)
    {
      InlineKeyboardButton linkbutton = InlineKeyboardButton.WithUrl("Lesen und Abstimmen", "https://t.me/" + roomname + "/" + messageId);
      List<InlineKeyboardButton> buttonlist = new List<InlineKeyboardButton>
      {
        linkbutton
      };
      return new InlineKeyboardMarkup(buttonlist);
    }

    /// <summary>
    /// [ Verstanden, Ausblenden ]
    /// </summary>
    /// <returns></returns>
    internal static InlineKeyboardMarkup getGotItDeleteButtonKeyboard()
    {
      InlineKeyboardButton gotItButton = InlineKeyboardButton.WithCallbackData("Verstanden, Ausblenden", DRaumManager.genericMessageDeletePrefix+"0");
      List<InlineKeyboardButton> buttonlist = new List<InlineKeyboardButton>
      {
        gotItButton
      };
      return new InlineKeyboardMarkup(buttonlist);
    }

    /// <summary>
    /// [ Beitrag löschen ] [ Flag entfernen ]
    /// </summary>
    /// <param name="postId">ID des geflaggten Posts</param>
    /// <returns></returns>
    internal static InlineKeyboardMarkup getFlaggedPostModKeyboard(long postId)
    {
      InlineKeyboardButton deleteButton = InlineKeyboardButton.WithCallbackData("Beitrag löschen", DRaumManager.modDeleteFlaggedPrefix + postId);
      InlineKeyboardButton clearFlagButton = InlineKeyboardButton.WithCallbackData("Flag entfernen", DRaumManager.modClearFlagPrefix + postId);
      List<InlineKeyboardButton> buttonlist = new List<InlineKeyboardButton>
      {
        deleteButton,
        clearFlagButton
      };
      return new InlineKeyboardMarkup(buttonlist);
    }

    /// <summary>
    /// [ Beitrag schreiben ] [ Feedback schreiben ]
    /// </summary>
    /// <returns></returns>
    internal static InlineKeyboardMarkup getChooseInputModeKeyboard()
    {
      InlineKeyboardButton writeButton = InlineKeyboardButton.WithCallbackData("Beitrag schreiben", DRaumManager.modeWritePrefix + "0");
      InlineKeyboardButton feedbackButton = InlineKeyboardButton.WithCallbackData("Feedback schreiben", DRaumManager.modeFeedbackPrefix + "0");
      List<InlineKeyboardButton> buttonlist = new List<InlineKeyboardButton>
      {
        writeButton,
        feedbackButton
      };
      return new InlineKeyboardMarkup(buttonlist);
    }

    /// <summary>
    /// [ Veröffentlichen ] [ Ablehnen ]
    /// </summary>
    /// <param name="postId">PostID des moderierten Posts, welches der User veröffentlichen oder ablehnen kann</param>
    /// <returns></returns>
    internal static InlineKeyboardMarkup getAcceptDeclineModeratedPostKeyboard(long postId)
    {
      InlineKeyboardButton acceptButton = InlineKeyboardButton.WithCallbackData("Veröffentlichen", DRaumManager.modAcceptPrefix + postId);
      InlineKeyboardButton cancelButton = InlineKeyboardButton.WithCallbackData("Ablehnen", DRaumManager.modBlockPrefix + postId);
      List<InlineKeyboardButton> buttonlist = new List<InlineKeyboardButton>
      {
        acceptButton,
        cancelButton
      };
      return new InlineKeyboardMarkup(buttonlist);
    }

    /// <summary>
    /// [ OK ] [ EDIT ] [ BLOCK ]
    /// </summary>
    /// <param name="postId">PostID des Posts der Moderiert werden soll</param>
    /// <returns></returns>
    internal static InlineKeyboardMarkup getModeratePostKeyboard(long postId)
    {
      InlineKeyboardButton okayButton = InlineKeyboardButton.WithCallbackData("OK", DRaumManager.modAcceptPrefix + postId);
      InlineKeyboardButton modifyButton = InlineKeyboardButton.WithCallbackData("EDIT", DRaumManager.modEditPrefix + postId);
      InlineKeyboardButton blockButton = InlineKeyboardButton.WithCallbackData("BLOCK", DRaumManager.modBlockPrefix + postId);
      List<InlineKeyboardButton> buttonlist = new List<InlineKeyboardButton>
      {
        okayButton,
        modifyButton,
        blockButton
      };
      return new InlineKeyboardMarkup(buttonlist);
    }


    /// <summary>
    /// [ 👍 x% ] [ 👎 y% ] [ 🚩 Melden ]
    /// </summary>
    /// <returns></returns>
    internal static InlineKeyboardMarkup getPostKeyboard(long posvotes, long negvotes, long postId)
    {
      InlineKeyboardButton thumbsUpButton = InlineKeyboardButton.WithCallbackData("👍 " + Utilities.getHumanAbbrevNumber(posvotes), DRaumManager.voteUpPrefix + postId);
      InlineKeyboardButton thumbsDownButton = InlineKeyboardButton.WithCallbackData("👎 " + Utilities.getHumanAbbrevNumber(negvotes), DRaumManager.voteDownPrefix + postId);
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