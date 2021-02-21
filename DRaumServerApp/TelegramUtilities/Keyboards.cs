using System.Collections.Generic;
using Telegram.Bot.Types.ReplyMarkups;

namespace DRaumServerApp.TelegramUtilities
{
  /// <summary>
  /// Stellt alle Inline-Keyboards zur Verfügung, die für den D-Raum verwendet werden
  /// </summary>
  internal static class Keyboards
  {

    internal static readonly string ModAcceptPrefix = "Y";
    internal static readonly string ModEditPrefix = "M";
    internal static readonly string ModBlockPrefix = "N";
    internal static readonly string ModGetNextCheckPostPrefix = "G";
    internal static readonly string ModDeleteFlaggedPrefix = "R";
    internal static readonly string ModClearFlagPrefix = "C";
    internal static readonly string ModeWritePrefix = "W";
    internal static readonly string ModeFeedbackPrefix = "B";
    internal static readonly string GenericMessageDeletePrefix = "X";
    internal static readonly string VoteUpPrefix = "U";
    internal static readonly string VoteDownPrefix = "D";
    internal static readonly string FlagPrefix = "F";

    private static InlineKeyboardMarkup _getNextPostToModerateKeyboard;
    private static InlineKeyboardMarkup _getGotItDeleteButtonKeyboard;
    private static InlineKeyboardMarkup _getChooseInputModeKeyboard;

    /// <summary>
    /// [ Beitrag laden ]
    /// </summary>
    /// <returns></returns>
    internal static InlineKeyboardMarkup getGetNextPostToModerateKeyboard()
    {
      return _getNextPostToModerateKeyboard ??= new InlineKeyboardMarkup(
        new List<InlineKeyboardButton>
        {
          InlineKeyboardButton.WithCallbackData("Beitrag laden", ModGetNextCheckPostPrefix + "0")
        });
    }

    /// <summary>
    /// [ Lesen und Abstimmen -> ]
    /// </summary>
    /// <param name="messageId">Msg-ID des Posts im Draum</param>
    /// <param name="roomname">Chat-Name des Draums für den Link</param>
    /// <returns></returns>
    internal static InlineKeyboardMarkup getTopPostLinkKeyboard(long messageId, string roomname)
    {
      return new InlineKeyboardMarkup(new List<InlineKeyboardButton>
      {
        InlineKeyboardButton.WithUrl("Lesen und Abstimmen", "https://t.me/" + roomname + "/" + messageId)
      });
    }

    /// <summary>
    /// [ Verstanden, Ausblenden ]
    /// </summary>
    /// <returns></returns>
    internal static InlineKeyboardMarkup getGotItDeleteButtonKeyboard()
    {
      return _getGotItDeleteButtonKeyboard ??= new InlineKeyboardMarkup(
        new List<InlineKeyboardButton>
        {
          InlineKeyboardButton.WithCallbackData("Verstanden, Ausblenden", GenericMessageDeletePrefix + "0")
        });
    }

    /// <summary>
    /// [ Beitrag löschen ] [ Flag entfernen ]
    /// </summary>
    /// <param name="postId">ID des geflaggten Posts</param>
    /// <returns></returns>
    internal static InlineKeyboardMarkup getFlaggedPostModKeyboard(long postId)
    {
      return new InlineKeyboardMarkup(new List<InlineKeyboardButton>
      {
        InlineKeyboardButton.WithCallbackData("Beitrag löschen", ModDeleteFlaggedPrefix + postId), InlineKeyboardButton.WithCallbackData("Flag entfernen", ModClearFlagPrefix + postId)
      });
    }

    /// <summary>
    /// [ Beitrag schreiben ] [ Feedback schreiben ]
    /// </summary>
    /// <returns></returns>
    internal static InlineKeyboardMarkup getChooseInputModeKeyboard()
    {
      return _getChooseInputModeKeyboard ??= new InlineKeyboardMarkup(
        new List<InlineKeyboardButton>
        {
          InlineKeyboardButton.WithCallbackData("Beitrag schreiben", ModeWritePrefix + "0"), InlineKeyboardButton.WithCallbackData("Feedback schreiben", ModeFeedbackPrefix + "0")
        });
    }

    /// <summary>
    /// [ Antworten ] [ Verwerfen ]
    /// </summary>
    /// <param name="chatId"></param>
    /// <returns></returns>
    internal static InlineKeyboardMarkup getFeedbackReplyKeyboard(long chatId)
    {
      return new InlineKeyboardMarkup(new List<InlineKeyboardButton>
      {
        InlineKeyboardButton.WithCallbackData("Antworten", Keyboards.ModAcceptPrefix + chatId),
        InlineKeyboardButton.WithCallbackData("Verwerfen", Keyboards.ModBlockPrefix + chatId)
      });
    }

    /// <summary>
    /// [ Veröffentlichen ] [ Ablehnen ]
    /// </summary>
    /// <param name="postId">PostID des moderierten Posts, welches der User veröffentlichen oder ablehnen kann</param>
    /// <returns></returns>
    internal static InlineKeyboardMarkup getAcceptDeclineModeratedPostKeyboard(long postId)
    {
      return new InlineKeyboardMarkup(new List<InlineKeyboardButton>
      {
        InlineKeyboardButton.WithCallbackData("Veröffentlichen", ModAcceptPrefix + postId), InlineKeyboardButton.WithCallbackData("Ablehnen", ModBlockPrefix + postId)
      });
    }

    /// <summary>
    /// [ OK ] [ EDIT ] [ BLOCK ]
    /// </summary>
    /// <param name="postId">PostID des Posts der Moderiert werden soll</param>
    /// <returns></returns>
    internal static InlineKeyboardMarkup getModeratePostKeyboard(long postId)
    {
      return new InlineKeyboardMarkup(new List<InlineKeyboardButton>
      {
        InlineKeyboardButton.WithCallbackData("OK", ModAcceptPrefix + postId),
        InlineKeyboardButton.WithCallbackData("EDIT", ModEditPrefix + postId),
        InlineKeyboardButton.WithCallbackData("BLOCK", ModBlockPrefix + postId)
      });
    }

    /// <summary>
    /// [ 👍 x ] [ 👎 y ] [ 🚩 Melden ]
    /// </summary>
    /// <returns></returns>
    internal static InlineKeyboardMarkup getPostKeyboard(long posvotes, long negvotes, long postId)
    {
      return new InlineKeyboardMarkup(new List<InlineKeyboardButton>
      {
        InlineKeyboardButton.WithCallbackData("👍 " + Utilities.getHumanAbbrevNumber(posvotes), VoteUpPrefix + postId),
        InlineKeyboardButton.WithCallbackData("👎 " + Utilities.getHumanAbbrevNumber(negvotes), VoteDownPrefix + postId),
        InlineKeyboardButton.WithCallbackData("🚩 Melden", FlagPrefix + postId)
      });
    }

  }
}