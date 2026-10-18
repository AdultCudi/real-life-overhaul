using System;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Flare
{
    /// <summary>The Flare app, what a click on a card does.</summary>
    public sealed partial class FlareApp
    {
        private void PostClicked(FlareRow row)
        {
            if (row?.Post != null && row.Post.Id >= 0 && !(row.Number == 1 && _at.Screen == FlareScreen.Thread))
            {
                Open(FlareScreen.Thread, 0, row.Post.Id);
            }
        }

        private void AuthorClicked(FlareRow row)
        {
            if (row?.Post == null)
            {
                return;
            }
            string key = FlareLiveRules.KeyOf(row.Post);
            if (key != null)
            {
                Open(FlareScreen.Profile, 0, -1, key);
            }
        }

        private void LikeClicked(FlareRow row)
        {
            if (row?.Post == null)
            {
                return;
            }
            if (row.Post.Id >= 0)
            {
                _director.Like(row.Post.Id);
            }
            else if (row.Post.Slot >= 0 && FlareCommentRules.IsDisplay(row.Post.Id))
            {
                _director.LikeComment(row.Post.ReplyTo, row.Post.Slot, FlareCommentRules.KindOf(row.Post.Author), row.Post.Who);   // a display-only comment: kept by its post, slot and who
            }
            else if (row.Post.Slot >= 0)
            {
                _director.LikeReply(row.Post.ReplyTo, row.Post.Slot);   // a friend's reply, made for display: kept by its post and place
            }
        }

        private void RepostClicked(FlareRow row)
        {
            if (row?.Post != null && row.Post.Id >= 0)
            {
                _director.Repost(row.Post.Id);
            }
        }

        private void ReplyClicked(FlareRow row)
        {
            if (row?.Post != null && row.Post.Id >= 0)
            {
                OpenCompose(-1, row.Post.Id);
            }
        }

        private void GoingClicked(FlareRow row)
        {
            if (row?.Post != null && row.Post.Id >= 0)
            {
                _director.Going(row.Post.Id);
            }
        }

        private void NoticeClicked(FlareRow row)
        {
            if (row == null)
            {
                return;
            }
            if (row.Notice.Kind == FlareNoticeKind.Dm && row.Dm != null)
            {
                Open(FlareScreen.Conversation, 0, -1, row.Dm.With);
                return;
            }
            FlarePost p = row.Post;
            if (p == null)
            {
                return;
            }
            bool reply = row.Notice.Kind == FlareNoticeKind.Reply || row.Notice.Kind == FlareNoticeKind.Case;
            Open(FlareScreen.Thread, 0, reply && p.ReplyTo >= 0 ? p.ReplyTo : p.Id);
        }

        private void AccountClicked(FlareRow row)
        {
            if (row?.Key != null)
            {
                Open(FlareScreen.Profile, 0, -1, row.Key);
            }
        }

        private void FollowAccount(FlareRow row)
        {
            FlareFile file = _director.File;
            if (file != null && row?.Key != null)
            {
                _director.Follow(row.Key, !FlareCast.Followed(file, row.Key));
            }
        }

        private void ConversationClicked(FlareRow row)
        {
            if (row?.Dm != null)
            {
                Open(FlareScreen.Conversation, 0, -1, row.Dm.With);
            }
        }

        private void AnnounceClicked(FlareRow row)
        {
            if (row?.Key != null)
            {
                OpenCompose(-1, -1);
                _compose.Kind = FlareSource.Announce;
                _compose.Place = row.Key;
                _compose.Name = row.Text;
                WriteSheet();
            }
        }

        private void PromoteClicked(FlareRow row)
        {
            if (row?.Key != null)
            {
                OpenHire(row.Key, row.Text, -1);
            }
        }

        private void EndDealClicked(FlareRow row)
        {
            if (row != null && row.Number > 0)
            {
                _director.EndHire(row.Number);
            }
        }

        private void RowButton(FlareRow row)
        {
            if (row == null)
            {
                return;
            }
            switch (row.Key)
            {
                case "reply":
                    OpenCompose(-1, row.Number);
                    break;
                case "write":
                    string with = _at.Key;
                    AskText("reallife_flare_dm_placeholder", "reallife_flare_send", null, FlareLiveRules.DmTextMost, text =>
                    {
                        if (_director.SendDm(with, text))
                        {
                            Refresh(false);
                        }
                    });
                    break;
                default:
                    OpenCompose(-1, -1);
                    break;
            }
        }

        /// <summary>The game's own text box, the one it names a business with, for what the player types.</summary>
        private static void AskText(string placeholderKey, string buttonKey, string initial, int most, Action<string> done)
        {
            TextBox.Ask(placeholderKey, buttonKey, text => UiClone.Safe(() => done(text)), null, initial, true, most);
        }
    }
}
