using System;
using System.Collections.Generic;
using System.IO;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;

namespace PresentMeTelegramBot
{
    class Program
    {
        internal static TelegramBotClient Bot;

        private enum ChatStates : byte { isWaitingForSenderContact, isWaitingForBirthDayAndMonth, isWaitingForFriendContact };

        private static List<string> commands = new List<string>() { "/start", "Как пользоваться", "\ud83c\udf81 Хочу подарок \ud83c\udf81", "\ud83c\udf81 Хочу подарить \ud83c\udf81" };

        private static Dictionary<int, ChatStates?> UsersChatStates = new Dictionary<int, ChatStates?>();

        static void Main(string[] args)
        {
            Bot = new TelegramBotClient(Config.TOKEN);

            Bot.OnMessage += BotOnMessageReceived;
            Bot.OnCallbackQuery += BotOnCallbackQueryReceived;

            TaskScheduler.Instance.ScheduleTask(0, 0, 24, () => Handler.SendPresentsOnBirthday());

            Bot.StartReceiving();
            Console.WriteLine("Program is running");
            Console.ReadKey();
            Bot.StopReceiving();
        }

        private static async void BotOnCallbackQueryReceived(object sender, CallbackQueryEventArgs e)
        {
            try
            {
                string buttonText = e.CallbackQuery.Data;

                if (buttonText == "Зарегистрироваться")
                {
                    UsersChatStates[e.CallbackQuery.From.Id] = ChatStates.isWaitingForSenderContact;
                    await Handler.RequestContact(Bot, e.CallbackQuery.From.Id);
                }
                else if (buttonText == "Как пользоваться")
                {
                    Handler.HowToUseMessage(e.CallbackQuery.From.Id);
                    await Handler.ShowMainMenu(Bot, e.CallbackQuery.From.Id);
                }
                else if (buttonText == "Хочу подарок")
                {
                    // Поиск телефона юзера, true - есть телефон, false - нету
                    bool isFound = DBContext.FindPhoneNumber(e.CallbackQuery.Message);
                    if (isFound)
                    {
                        //isWaitingForGift = true;
                        //isWaitingForNumber = false;
                        await Handler.WantPresentHandler(Bot, e.CallbackQuery.From.Id);
                    }
                    else
                    {
                        UsersChatStates[e.CallbackQuery.From.Id] = ChatStates.isWaitingForSenderContact;
                        await Handler.RequestContact(Bot, e.CallbackQuery.From.Id);
                    }
                }
                else if (buttonText == "Хочу подарить")
                {
                    UsersChatStates[e.CallbackQuery.From.Id] = ChatStates.isWaitingForFriendContact;
                    await Handler.WantToGive(Bot, e.CallbackQuery.From.Id);
                }
                else if (buttonText == "тест кнопка для отправки подарков")
                {
                    Handler.TESTSendPresentsOnBirthday();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static async void BotOnMessageReceived(object sender, MessageEventArgs e)
        {
            try
            {
                if (e.Message == null)
                    return;
                int senderUserId = e.Message.From.Id;

                UsersChatStates.TryGetValue(senderUserId, out ChatStates? chatState);

                if (commands.Contains(e.Message.Text) && chatState != null)
                {
                    UsersChatStates.Remove(senderUserId);
                    chatState = null;
                }

                if (e.Message.Type == MessageType.Text && chatState != ChatStates.isWaitingForSenderContact
                                                       && chatState != ChatStates.isWaitingForFriendContact)
                {
                    if (e.Message.Text == "/start")
                    {
                        if (!DBContext.FindPhoneNumber(e.Message))
                        {
                            await Handler.ShowStartMenu(Bot, senderUserId);
                            await Handler.ShowMainMenu(Bot, senderUserId);
                        }
                        else
                        {
                            await Handler.WantPresentHandler(Bot, senderUserId);
                            await Handler.ShowMainMenu(Bot, senderUserId);
                        }
                    }
                    else if (chatState == ChatStates.isWaitingForBirthDayAndMonth)
                    {
                        if (await Handler.SaveBirthDayAndMonth(Bot, e.Message))
                        {
                            UsersChatStates.Remove(senderUserId);
                            await Bot.SendTextMessageAsync(senderUserId, "Вы успешно зарегистрировались");
                            await Handler.WantPresentHandler(Bot, e.Message.From.Id);
                            await Handler.ShowMainMenu(Bot, senderUserId);
                        }
                    }
                    else if (e.Message.Text == "Как пользоваться")
                    {
                        Handler.HowToUseMessage(senderUserId);
                        UsersChatStates.Remove(senderUserId);
                        //await Handler.ShowMainMenu(Bot, senderUserId);
                    }
                    else if (e.Message.Text == "\ud83c\udf81 Хочу подарок \ud83c\udf81")
                    {
                        // Поиск телефона юзера, true - есть телефон, false - нету
                        bool isFound = DBContext.FindPhoneNumber(e.Message);
                        if (isFound)
                        {
                            //isWaitingForGift = true;
                            //isWaitingForNumber = false;
                            await Handler.WantPresentHandler(Bot, senderUserId);
                            //await Handler.ShowMainMenu(Bot, senderUserId);
                        }
                        else
                        {
                            UsersChatStates[senderUserId] = ChatStates.isWaitingForSenderContact;
                            await Handler.RequestContact(Bot, senderUserId);
                        }
                    }
                    else if (e.Message.Text == "\ud83c\udf81 Хочу подарить \ud83c\udf81")
                    {
                        UsersChatStates[senderUserId] = ChatStates.isWaitingForFriendContact;
                        await Handler.WantToGive(Bot, senderUserId);
                    }
                    else
                    {
                        await DBContext.SaveTextOrLinkToDb(Bot, e.Message);
                        //await Handler.ShowMainMenu(Bot, senderUserId);
                    }
                }
                else if (e.Message.Type == MessageType.Text && chatState == ChatStates.isWaitingForSenderContact)
                {
                    string msg = "Извините, но для отправки своего номера телефона, Вы должны нажать на соответствующую кнопку на клавиатуре:";
                    string promptImg = "keyboardPrompt.jpg";
                    using (FileStream fileStream = new FileStream(promptImg, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        await Bot.SendTextMessageAsync(senderUserId, msg);
                        await Bot.SendPhotoAsync(senderUserId, new InputOnlineFile(fileStream, promptImg));
                    }
                }
                else if (e.Message.Type == MessageType.Text && chatState == ChatStates.isWaitingForFriendContact)
                {
                    string msg = "Извините, но для просмотра желаемых подарков друга, Вы должны прикрепить контакт друга из списка контактов";
                    await Bot.SendTextMessageAsync(senderUserId, msg);
                }
                else if (e.Message.Type == MessageType.Contact)
                {
                    if (chatState == ChatStates.isWaitingForSenderContact)
                    {
                        if (e.Message.Contact.UserId == senderUserId)
                        {
                            await DBContext.SaveContactToDb(Bot, e.Message.Contact);
                            UsersChatStates[senderUserId] = ChatStates.isWaitingForBirthDayAndMonth;
                            string msg = "Теперь напишите пожалуйста Ваш день рождения, только день и месяц, указав их порядковые номера, например: 16 04 (16 апреля).";
                            await Bot.SendTextMessageAsync(senderUserId, msg);
                        }
                        else
                        {
                            string msg = "Извините, Вы должны отправить свой контакт, а не контакт друга.";
                            await Bot.SendTextMessageAsync(senderUserId, msg);
                        }
                    }
                    else
                    {
                        if (DBContext.FindPhoneNumber(e.Message.Contact.PhoneNumber))
                            await DBContext.GetPresentsByPhoneNumber(Bot, e.Message);
                        else
                            Handler.FriendNotFound(senderUserId);

                        //await Handler.ShowMainMenu(Bot, senderUserId);
                        UsersChatStates.Remove(senderUserId);
                    }
                }
                else if (e.Message.Type == MessageType.Photo)
                {
                    await DBContext.SaveImageUri(Bot, e.Message);
                }
                else
                {
                    await Bot.SendTextMessageAsync(senderUserId, "Извините, что-то пошло не так");
                    //await Handler.ShowMainMenu(Bot, senderUserId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
