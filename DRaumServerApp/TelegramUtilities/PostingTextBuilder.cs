using DRaumServerApp.Authors;
using DRaumServerApp.Postings;
using System.Text;

namespace DRaumServerApp.TelegramUtilities
{
  internal class PostingTextBuilder
  {
    private readonly PostingManager posts;
    private readonly AuthorManager authors;

    internal PostingTextBuilder(PostingManager postingManager, AuthorManager authorManager)
    {
      this.posts = postingManager;
      this.authors = authorManager;
    }

    internal string buildPostingTextForTopTeaser(long postingId)
    {
      StringBuilder sb = new StringBuilder();
      if (this.posts.isTopPost(postingId))
      {
        sb.Append("<b>🔈 == TOP-POST Nr. ");
        sb.Append(postingId);
        sb.Append(" == 🔈</b>\r\n\r\n");
      }
      else
      {
        sb.Append("<b>Post Nr. ");
        sb.Append(postingId);
        sb.Append("</b>\r\n\r\n");
      }
      sb.Append(this.posts.getPostingText(postingId).Substring(0, 60));
      sb.Append(" [...]");
      sb.Append("\r\n\r\n");
      sb.Append(this.authors.getAuthorPostText(this.posts.getAuthorId(postingId)));
      sb.Append("\r\n");
      sb.Append(this.posts.getPostingStatisticText(postingId));
      return sb.ToString();
    }

    internal string buildPostingText(long postingId)
    {
      StringBuilder sb = new StringBuilder();
      if (this.posts.isTopPost(postingId))
      {
        sb.Append("<b>🔈 == TOP-POST Nr. ");
        sb.Append(postingId);
        sb.Append(" == 🔈</b>\r\n\r\n");
      }
      else
      {
        sb.Append("<b>Post Nr. ");
        sb.Append(postingId);
        sb.Append("</b>\r\n\r\n");
      }
      sb.Append(this.posts.getPostingText(postingId));
      sb.Append("\r\n\r\n");
      sb.Append(this.authors.getAuthorPostText(this.posts.getAuthorId(postingId)));
      sb.Append("\r\n");
      sb.Append(this.posts.getPostingStatisticText(postingId));
      return sb.ToString();
    }




  }

}