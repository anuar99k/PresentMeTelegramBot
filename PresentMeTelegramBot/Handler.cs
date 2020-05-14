using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace PresentMeTelegramBot
{
    public class Handler
    {
        public static async Task ShowStartMenu(TelegramBotClient bot, int userId)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Если Вы хотите пополнить Ваш список желаемых подарков, то:\n");
            sb.Append("1) Нажмите «Зарегистрироваться»\n");
            sb.Append("2) Отправьте свой номер телефона, нажав на соответствующую кнопку на клавиатуре\n");
            sb.Append("3) Отправьте свой день и месяц рождения, для того чтобы Ваши друзья могли знать, что Вы хотели бы получить на свой день рождения.\n");
            sb.Append("4) Отправляйте в чат ссылки с любых сайтов, картинки или текстовое описание подарка.\n\n");
            sb.Append("Если Вы хотите узнать список желаемых подарков Вашего друга, то:\n");
            sb.Append("1) Нажмите «Хочу подарить»\n");
            sb.Append("2) Прикрепите контакт Вашего друга, из Вашего списка контактов\n");
            sb.Append("3) Получите список подарков, которые хочет получить Ваш друг\n");

            var inlineKeyBoard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Зарегистрироваться") },
                    //new[] { InlineKeyboardButton.WithCallbackData("Хочу подарить") },
                });
            await bot.SendTextMessageAsync(userId, sb.ToString(), replyMarkup: inlineKeyBoard);
        }

        internal static void HowToUseMessage(int userId)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Если Вы хотите пополнить Ваш список желаемых подарков, то просто ");
            sb.Append("отправляйте в чат ссылки с любых сайтов, картинки или текстовое описание подарка.\n\n");
            sb.Append("Если Вы хотите узнать список желаемых подарков Вашего друга, то:\n");
            sb.Append("1) Нажмите «Хочу подарить»\n");
            sb.Append("2) Прикрепите контакт Вашего друга, из Вашего списка контактов\n");
            sb.Append("3) Получите список подарков, которые хочет получить Ваш друг\n");

            Program.Bot.SendTextMessageAsync(userId, sb.ToString());
        }

        internal static void SendPresentsOnBirthday()
        {
            IEnumerable<IGrouping<int, DataRow>> usersAndFriends = DBContext.GetUserAndFriendsByBirthday();

            foreach (IGrouping<int, DataRow> userIdAndFriends in usersAndFriends)
            {
                DBContext.GetAndSendPresentsForBirthday(userIdAndFriends);
            }
        }

        internal static void TESTSendPresentsOnBirthday()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("SELECT * FROM UserFriends ");

            SQLiteDataAdapter da = new SQLiteDataAdapter(sb.ToString(), "Data Source=DB.db; Version=3");
            DataSet ds = new DataSet();
            da.Fill(ds);

            IEnumerable<IGrouping<int, DataRow>> usersAndFriends = ds.Tables[0].Rows.Cast<DataRow>().GroupBy(g => Convert.ToInt32(g[0]));

            foreach (IGrouping<int, DataRow> userIdAndFriends in usersAndFriends)
            {
                DBContext.GetAndSendPresentsForBirthday(userIdAndFriends);
            }
        }

        public static async Task ShowMainMenu(TelegramBotClient bot, int userId)
        {
            //var inlineKeyBoard = new InlineKeyboardMarkup(new[]
            //    {
            //        new[] { InlineKeyboardButton.WithCallbackData("Как пользоваться") },
            //        new[] { InlineKeyboardButton.WithCallbackData("Хочу подарить") },
            //        new[] { InlineKeyboardButton.WithSwitchInlineQuery("Поделиться с друзьями") },
            //        new[] { InlineKeyboardButton.WithCallbackData("тест кнопка для отправки подарков") }
            //    });
            var replyKeyBoard = new ReplyKeyboardMarkup(new[]
                {
                    new [] { new KeyboardButton("Как пользоваться") },
                    new [] { new KeyboardButton("\ud83c\udf81 Хочу подарок \ud83c\udf81") },
                    new [] { new KeyboardButton("\ud83c\udf81 Хочу подарить \ud83c\udf81") }
                }, resizeKeyboard: true);
            string msg = "Главное меню &#11015;";
            await bot.SendTextMessageAsync(userId, msg, parseMode: ParseMode.Html, replyMarkup: replyKeyBoard);
        }

        public static async Task WantPresentHandler(TelegramBotClient bot, int userId)
        {
            //string msg = "Теперь Вы можете отправлять сюда ссылки на желаемые подарки, а также фотографии или текстовое описание подарка. Затем Вы можете поделиться ссылкой на бота с Вашими друзьями, чтобы они могли посмотреть Ваш список желаемых подарков";
            string msg = "Если Вы хотите пополнить Ваш список желаемых подарков, то просто отправляйте в чат ссылки с любых сайтов, картинки или текстовое описание подарка.";
            await bot.SendTextMessageAsync(userId, msg);
        }

        internal static async Task RequestContact(TelegramBotClient bot, int userId)
        {
            var replyKeyBoard = new ReplyKeyboardMarkup(new[]
                {
                    new [] { new KeyboardButton("Поделиться своим контактом") { RequestContact = true } },
                    new [] { new KeyboardButton("\ud83c\udf81 Хочу подарить \ud83c\udf81") },
                }, resizeKeyboard: true);
            string msg = "Для сохранения подарков, Вы должны зарегистрироваться, отправив нам свой контакт, нажав соответсвующую кнопку на клавиатуре.";
            await bot.SendTextMessageAsync(userId, msg, replyMarkup: replyKeyBoard);
        }

        internal static async Task WantToGive(TelegramBotClient bot, int userId)
        {
            string msg = "Прикрепите контакт Вашего друга из списка контактов";
            await bot.SendTextMessageAsync(userId, msg);
        }

        internal static async Task<bool> SaveBirthDayAndMonth(TelegramBotClient bot, Message message)
        {
            string birthDayAndMonth = message.Text.Trim();
            if (birthDayAndMonth.Split(' ').Length != 2)
            {
                await bot.SendTextMessageAsync(message.From.Id, "Некорректная дата");
                return false;
            }
            if (!int.TryParse(birthDayAndMonth.Split(' ')[0], out int day) || !int.TryParse(birthDayAndMonth.Split(' ')[1], out int month))
            {
                await bot.SendTextMessageAsync(message.From.Id, "День и месяц даты Вашего рождения должны быть в цифровом эквиваленте");
                return false;
            }
            try
            {
                // проверка валидности даты
                DateTime dt = new DateTime(1900, month, day);

                DBContext.SaveBirthDayAndMonth(day + " " + month, message.From.Id);

                await bot.SendTextMessageAsync(message.From.Id, "Ваша дата рождения сохранена");

                return true;
            }
            catch (Exception)
            {
                await bot.SendTextMessageAsync(message.From.Id, "Некорректная дата");
                return false;
            }
        }

        internal static async void RegistrationRequest(int userId)
        {
            string msg = "Извините, но для сохранения подарков, Вы сначала должны пройти регистрацию";
            var inlineKeyBoard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Зарегистрироваться") }
                });
            await Program.Bot.SendTextMessageAsync(userId, msg, replyMarkup: inlineKeyBoard);
        }

        internal static void FriendNotFound(int userId)
        {
            string msg = "Номер телефона Вашего друга не найден, поделитесь ссылкой на бота, чтобы поскорее узнать желаемые подарки друга";
            string sharingText = "Привет! Отправляй боту то, что хочешь получить в подарок, и заодно посмотри, что хочу получить в подарок я";
            var inlineKeyBoard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithSwitchInlineQuery("Поделиться с друзьями", sharingText) }
                });
            Program.Bot.SendTextMessageAsync(userId, msg, replyMarkup: inlineKeyBoard);
        }
    }
}
