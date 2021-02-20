using System;
using Telegram.Bot;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace DRaumServerApp.telegram
{
  internal class AdminBot
  {
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();


    private readonly AuthorManager authors;
    private readonly TelegramBotClient telegramAdminBot;
    private readonly DRaumStatistics statistics;
    private readonly PostingManager posts;
    private readonly FeedbackManager feedbackManager;

    internal AdminBot(AuthorManager authors, DRaumStatistics statistics, TelegramBotClient telegramAdminBot, PostingManager posts, FeedbackManager feedbackManager)
    {
      this.authors = authors;
      this.statistics = statistics;
      this.telegramAdminBot = telegramAdminBot;
      this.posts = posts;
      this.feedbackManager = feedbackManager;
    }

    internal async Task removeMessage(int messageId, long adminChatId)
    {
      try
      {
        await this.telegramAdminBot.DeleteMessageAsync(
          messageId: messageId,
          chatId: adminChatId
        ).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim löschen einer Nachricht an den Admin, Msg-ID: " + messageId);
      }
    }


    internal async Task<Message> sendMessage(long chatId, string message)
    {
      try
      {
        return await this.telegramAdminBot.SendTextMessageAsync(
          chatId: chatId,
          text: message
        );
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Senden einer Nachricht an den Admin, chatid: " + chatId);
      }
      return null;
    }

    internal async Task editMessage(long chatId, int messageId, string message)
    {
      try
      {
        await this.telegramAdminBot.EditMessageTextAsync(
          chatId: chatId,
          messageId: messageId,
          text: message);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Aktualisieren einer Nachricht an den Admin, chatid: " + chatId);
      }
    }

    internal async Task replyToCallback(string callbackId, string message)
    {
      try
      {
        await this.telegramAdminBot.AnswerCallbackQueryAsync(
          callbackQueryId: callbackId,
          text: message,
          showAlert: true);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Senden der Callback-Antwort an den Admin, message: " + message);
      }
    }

    internal async Task<Message> sendMessageWithKeyboard(long chatId, string message, InlineKeyboardMarkup keyboard)
    {
      try
      {
        return await this.telegramAdminBot.SendTextMessageAsync(
          chatId: chatId,
          text: message,
          replyMarkup: keyboard
        );
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Senden einer Nachricht (Mit Inline-Keyboard) an den Admin, chatid: " + chatId);
      }
      return null;
    }




  }
}