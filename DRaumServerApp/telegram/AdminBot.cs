using Telegram.Bot;

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


  }
}