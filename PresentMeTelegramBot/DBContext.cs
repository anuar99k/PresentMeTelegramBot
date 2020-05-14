using PresentMeTelegramBot.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;

namespace PresentMeTelegramBot
{
    class DBContext
    {
        private static SQLiteDataAdapter da { get; set; }
        private static DataSet ds { get; set; }
        private static string CS = Config.CS;
        private static SQLiteCommandBuilder SCB { get; set; }

        internal static bool FindPhoneNumber(Message message)
        {
            int userId = message.From.Id;

            da = new SQLiteDataAdapter("SELECT * FROM User", CS);
            ds = new DataSet();
            da.Fill(ds);

            List<DataRow> rows = ds.Tables[0].Rows.Cast<DataRow>().ToList();
            DataRow userRow = rows.FirstOrDefault(f => Convert.ToInt32(f[1]) == userId);

            if (userRow != null)
            {
                if (!string.IsNullOrEmpty(Convert.ToString(userRow[0])))
                    return true;
            }
            else
            {
                DataRow newUserRow = ds.Tables[0].NewRow();
                newUserRow[1] = userId;
                newUserRow["Username"] = message.From.Username;
                ds.Tables[0].Rows.Add(newUserRow);
                SCB = new SQLiteCommandBuilder(da);
                da.Update(ds);
            }
            return false;
        }

        internal static bool FindPhoneNumber(string phoneNumber)
        {
            phoneNumber = phoneNumber.Substring(phoneNumber.Length - 10);

            da = new SQLiteDataAdapter($"SELECT * FROM User WHERE PhoneNumber = {phoneNumber}", CS);
            ds = new DataSet();
            da.Fill(ds);

            if (ds.Tables[0].Rows.Count == 0)
                return false;

            DataRow userRow = ds.Tables[0].Rows[0];

            if (userRow != null)
            {
                if (!string.IsNullOrEmpty(Convert.ToString(userRow[0])))
                    return true;
            }
            return false;
        }

        internal static async Task GetAndSendPresentsForBirthday(IGrouping<int, DataRow> userIdAndFriends)
        {
            int receiverUserId = userIdAndFriends.Key;
            List<int> birthdayFriendsId = userIdAndFriends.Select(s => Convert.ToInt32(s["FriendId"])).ToList();

            StringBuilder sqlQueryBuilder = new StringBuilder();
            sqlQueryBuilder.Append("SELECT * FROM TextOrLink");
            sqlQueryBuilder.Append($" WHERE UserId IN ({string.Join(',', birthdayFriendsId)});");
            sqlQueryBuilder.Append("SELECT * FROM ImageUri");
            sqlQueryBuilder.Append($" WHERE UserId IN ({string.Join(',', birthdayFriendsId)});");
            sqlQueryBuilder.Append("SELECT UserId, Username, PhoneNumber FROM User");
            sqlQueryBuilder.Append($" WHERE UserId IN ({string.Join(',', birthdayFriendsId)});");

            da = new SQLiteDataAdapter(sqlQueryBuilder.ToString(), CS);
            ds = new DataSet();
            da.Fill(ds);

            DataTable textOrLinkTable = ds.Tables[0];
            DataTable imageUriTable = ds.Tables[1];
            DataTable userTable = ds.Tables[2];

            List<DataRow> textOrLinkRows = textOrLinkTable.Rows.Cast<DataRow>().ToList();
            List<DataRow> imageUriRows = imageUriTable.Rows.Cast<DataRow>().ToList();
            List<DataRow> userRows = userTable.Rows.Cast<DataRow>().ToList();

            foreach (int birthdayFriendId in birthdayFriendsId)
            {
                List<DataRow> friendTextOrLinkRows = textOrLinkRows.Where(w => Convert.ToInt32(w["UserId"]) == birthdayFriendId).ToList();
                List<DataRow> friendImageUriRows = imageUriRows.Where(w => Convert.ToInt32(w["UserId"]) == birthdayFriendId).ToList();

                DataRow birthdayFriendRow = userRows.First(f => Convert.ToInt32(f["UserId"]) == birthdayFriendId);
                string birthdayFriendUsername = Convert.ToString(birthdayFriendRow["Username"]);
                string birthdayFriendPhoneNumber = Convert.ToString(birthdayFriendRow["PhoneNumber"]);

                if (friendTextOrLinkRows.Count == 0 && friendImageUriRows.Count == 0)
                {
                    if (!string.IsNullOrEmpty(birthdayFriendUsername))
                        await Program.Bot.SendTextMessageAsync(receiverUserId, $"У Вашего друга @{birthdayFriendUsername} через 3 дня день рождения!\nК сожалению, его список желаемых подарков пуст :(");
                    else
                        await Program.Bot.SendTextMessageAsync(receiverUserId, $"У Вашего друга под номером 8{birthdayFriendPhoneNumber} через 3 дня день рождения!\nК сожалению, его список желаемых подарков пуст :(");
                    return;
                }
                StringBuilder msgBuilder = new StringBuilder();

                foreach (DataRow textOrLinkRow in friendTextOrLinkRows)
                {
                    msgBuilder.Append(Convert.ToString(textOrLinkRow[1]) + "\n\n\n");
                }

                if (!string.IsNullOrEmpty(birthdayFriendUsername))
                    await Program.Bot.SendTextMessageAsync(receiverUserId, $"У Вашего друга @{birthdayFriendUsername} через 3 дня день рождения!\nВот список его желаемых подарков :)");
                else
                    await Program.Bot.SendTextMessageAsync(receiverUserId, $"У Вашего друга под номером 8{birthdayFriendPhoneNumber} через 3 дня день рождения!\nВот список его желаемых подарков :)");
                if (!string.IsNullOrEmpty(msgBuilder.ToString()))
                    await Program.Bot.SendTextMessageAsync(receiverUserId, msgBuilder.ToString());

                if (friendImageUriRows.Count != 0)
                    await Program.Bot.SendTextMessageAsync(receiverUserId, "Загрузка картинок...");
                else
                    return;

                foreach (DataRow imageUriRow in friendImageUriRows)
                {
                    string imageUri = Convert.ToString(imageUriRow[1]);
                    string caption = null;
                    if (imageUriRow[3] != null)
                        caption = Convert.ToString(imageUriRow[3]);
                    using (FileStream fs = new FileStream(imageUri, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        await Program.Bot.SendPhotoAsync(receiverUserId, new InputOnlineFile(fs), caption: caption);
                    }
                }
                await Program.Bot.SendTextMessageAsync(receiverUserId, "Картинки высланы");
            }
        }

        internal static IEnumerable<IGrouping<int, DataRow>> GetUserAndFriendsByBirthday()
        {
            string dayAndMonthAfterThreeDays = DateTime.Now.AddDays(3).Day + " " + DateTime.Now.AddDays(3).Month;
            StringBuilder sb = new StringBuilder();
            sb.Append("SELECT * FROM UserFriends ");
            sb.Append("WHERE FriendId IN (SELECT FriendId FROM User ");
            sb.Append($"WHERE BirthDayAndMonth = '{dayAndMonthAfterThreeDays}')");

            da = new SQLiteDataAdapter(sb.ToString(), CS);
            ds = new DataSet();
            da.Fill(ds);

            return ds.Tables[0].Rows.Cast<DataRow>().GroupBy(g => Convert.ToInt32(g[0]));
        }

        internal static async Task SaveContactToDb(TelegramBotClient bot, Contact contact)
        {
            da = new SQLiteDataAdapter("SELECT * FROM User", CS);
            ds = new DataSet();
            da.Fill(ds);

            List<DataRow> rows = ds.Tables[0].Rows.Cast<DataRow>().ToList();
            DataRow userRow = rows.FirstOrDefault(f => Convert.ToInt32(f["UserId"]) == contact.UserId);

            if (userRow != null)
            {
                string msg = "";
                if (string.IsNullOrEmpty(Convert.ToString(userRow[0])))
                {
                    string phoneNumber = contact.PhoneNumber;
                    phoneNumber = phoneNumber.Substring(phoneNumber.Length - 10);
                    userRow["PhoneNumber"] = phoneNumber;
                    SCB = new SQLiteCommandBuilder(da);
                    da.Update(ds);
                    msg = "Ваш контакт успешно добавлен";
                }
                else
                {
                    msg = "Ваш контакт уже существует";
                    await bot.SendTextMessageAsync(contact.UserId, msg);
                }
            }
        }

        // метод вызывается в случае наличия номера телефона друга в базе
        internal static async Task GetPresentsByPhoneNumber(TelegramBotClient bot, Message message)
        {
            int senderUserId = message.From.Id;
            string phoneNumber = message.Contact.PhoneNumber;
            phoneNumber = phoneNumber.Substring(phoneNumber.Length - 10);

            StringBuilder sqlQueryBuilder = new StringBuilder();
            sqlQueryBuilder.Append("SELECT UserId FROM User");
            sqlQueryBuilder.Append($" WHERE PhoneNumber = {phoneNumber};");
            sqlQueryBuilder.Append("SELECT * FROM TextOrLink");
            sqlQueryBuilder.Append($" WHERE UserId = (SELECT UserId FROM User WHERE PhoneNumber = {phoneNumber});");
            sqlQueryBuilder.Append("SELECT * FROM ImageUri");
            sqlQueryBuilder.Append($" WHERE UserId = (SELECT UserId FROM User WHERE PhoneNumber = {phoneNumber});");
            sqlQueryBuilder.Append($" SELECT * FROM UserFriends WHERE UserId = {senderUserId}");

            da = new SQLiteDataAdapter(sqlQueryBuilder.ToString(), CS);
            ds = new DataSet();
            da.Fill(ds);

            int friendId = Convert.ToInt32(ds.Tables[0].Rows[0][0]);
            DataTable textOrLinkTable = ds.Tables[1];
            DataTable imageUriTable = ds.Tables[2];
            DataTable userFriendsTable = ds.Tables[3];

            List<DataRow> textOrLinkRows = textOrLinkTable.Rows.Cast<DataRow>().ToList();
            List<DataRow> imageUriRows = imageUriTable.Rows.Cast<DataRow>().ToList();
            List<DataRow> userFriendsRows = userFriendsTable.Rows.Cast<DataRow>().ToList();

            // добавление записи в таблицу UserFriends, если такой записи нет
            if (userFriendsRows.Count != 0 && !userFriendsRows.Any(a => Convert.ToInt32(a[1]) == friendId))
            {
                using (SQLiteConnection con = new SQLiteConnection(CS))
                {
                    con.Open();
                    da.InsertCommand = new SQLiteCommand($"INSERT INTO UserFriends (UserId, FriendId) VALUES ({senderUserId}, {friendId})", con);
                    da.InsertCommand.ExecuteNonQuery();
                }
            }

            if (textOrLinkRows.Count == 0 && imageUriRows.Count == 0)
            {
                await bot.SendTextMessageAsync(senderUserId, $"Список желаемых подарков Вашего друга пуст");
                return;
            }

            StringBuilder msgBuilder = new StringBuilder();

            foreach (DataRow textOrLinkRow in textOrLinkRows)
            {
                msgBuilder.Append(Convert.ToString(textOrLinkRow[1]) + "\n\n\n");
            }
            await bot.SendTextMessageAsync(senderUserId, "Желаемые подарки Вашего друга");
            if (!string.IsNullOrEmpty(msgBuilder.ToString()))
                await bot.SendTextMessageAsync(senderUserId, msgBuilder.ToString());

            if (imageUriRows.Count != 0)
                await bot.SendTextMessageAsync(senderUserId, "Загрузка картинок...");
            else
            {
                await bot.SendTextMessageAsync(senderUserId, "Список желаемых подарков Вашего друга выслан");
                return;
            }
            //List<InputMediaBase> inputMedia = new List<InputMediaBase>();
            foreach (DataRow imageUriRow in imageUriRows)
            {
                string imageUri = Convert.ToString(imageUriRow[1]);
                string caption = null;
                if (imageUriRow[3] != null)
                    caption = Convert.ToString(imageUriRow[3]);
                using (FileStream fs = new FileStream(imageUri, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    //inputMedia.Add(new InputMediaPhoto(new InputMedia(fs, "img")));
                    await bot.SendPhotoAsync(senderUserId, new InputOnlineFile(fs), caption: caption);
                }
            }
            //await bot.SendMediaGroupAsync(userId, inputMedia);
            await bot.SendTextMessageAsync(senderUserId, "Список желаемых подарков Вашего друга выслан");
        }

        internal static void SaveBirthDayAndMonth(string birthDayAndMonth, int userId)
        {
            string sqlQuery = $"UPDATE User SET BirthDayAndMonth = '{birthDayAndMonth}' WHERE UserId = {userId}";
            using (SQLiteConnection con = new SQLiteConnection(CS))
            {
                con.Open();
                SQLiteCommand cmd = new SQLiteCommand(sqlQuery, con);
                cmd.ExecuteNonQuery();
            }
        }

        internal static async Task SaveTextOrLinkToDb(TelegramBotClient bot, Message message)
        {
            bool isUserRegistered = FindPhoneNumber(message);
            if (isUserRegistered)
            {
                da = new SQLiteDataAdapter("SELECT * FROM TextOrLink", CS);
                ds = new DataSet();
                da.Fill(ds);
                List<DataRow> rows = ds.Tables[0].Rows.Cast<DataRow>().ToList();
                string msg = "";
                if (!rows.Any(a => Convert.ToString(a["TextOrLink"]) == message.Text))
                {
                    try
                    {
                        DataRow newTextOrLinkRow = ds.Tables[0].NewRow();
                        newTextOrLinkRow["UserId"] = message.From.Id;
                        newTextOrLinkRow["TextOrLink"] = message.Text;
                        ds.Tables[0].Rows.Add(newTextOrLinkRow);
                        SCB = new SQLiteCommandBuilder(da);
                        da.Update(ds);
                        msg = "Сохранено";
                    }
                    catch { msg = "Извините, что-то пошло не так, попробуйте отправить еще раз."; }
                }
                else
                    msg = "Данное описание подарка, или ссылка уже существует";
                await bot.SendTextMessageAsync(message.From.Id, msg);
            }
            else
                Handler.RegistrationRequest(message.From.Id);
        }

        internal static async Task SaveImageUri(TelegramBotClient bot, Message message)
        {
            bool isUserRegistered = FindPhoneNumber(message);
            if (isUserRegistered)
            {
                var photo = await bot.GetFileAsync(message.Photo.Last().FileId);

                string imageSource = $"https://api.telegram.org/file/bot{Config.TOKEN}/{photo.FilePath}";
                string imgExtension = photo.FilePath.Split('.').Last();
                string imageUri = $@"images\{message.From.Id}\{photo.FileUniqueId}.{imgExtension}";

                if (!Directory.Exists($@"images\{message.From.Id}"))
                    Directory.CreateDirectory($@"images\{message.From.Id}");

                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(new Uri(imageSource), imageUri);
                }

                da = new SQLiteDataAdapter("SELECT * FROM ImageUri", CS);
                ds = new DataSet();
                da.Fill(ds);

                List<DataRow> rows = ds.Tables[0].Rows.Cast<DataRow>().ToList();
                string msg = "";
                if (!rows.Any(a => Convert.ToString(a["ImageUri"]) == imageUri))
                {
                    try
                    {
                        DataRow newImageUri = ds.Tables[0].NewRow();
                        newImageUri["UserId"] = message.From.Id;
                        newImageUri["ImageUri"] = imageUri;
                        if (!string.IsNullOrEmpty(message.Caption))
                            newImageUri["Caption"] = message.Caption;
                        ds.Tables[0].Rows.Add(newImageUri);
                        SCB = new SQLiteCommandBuilder(da);
                        da.Update(ds);
                        msg = "Картинка сохранена";
                    }
                    catch { msg = "Извините, что-то пошло не так, попробуйте отправить картинку еще раз."; }
                }
                else
                    msg = "Такая картинка уже существует";
                await bot.SendTextMessageAsync(message.From.Id, msg);
            }
            else
                Handler.RegistrationRequest(message.From.Id);
        }
    }
}
