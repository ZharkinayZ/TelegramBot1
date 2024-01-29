using System;
using System.IO;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot
{
    internal class TelegraBotHelper
    {
        private const string Product = "Товары";
        private const string Basket = "Корзина";
        private const string Order = "Заказать";
        private const string History = "Заказы";

        private const string Tomato = "Помидоры";
        private const string Cabbage = "Капуста";

        private const string Start = "/start";

        private const string Plus = "increase_counter";
        private const string Minus = "decrement _counter";
        private const string Counter = "counter";


        private string _token;
        private TelegramBotClient _client;
        private Dictionary<long, UserState> _clientStates = new Dictionary<long, UserState>();
        private CancellationTokenSource _srcToken = new();
        internal TelegraBotHelper(string token)
        {
            this._token = token;
        }

        internal void StartReceiving()
        {
            _client = new TelegramBotClient(_token);

            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            ReceiverOptions receiverOptions = new()
            {
                ThrowPendingUpdates = true,
                AllowedUpdates = Array.Empty<UpdateType>() // receive all update types except ChatMember related updates
            };

            _client.StartReceiving(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: _srcToken.Token
            );
        }

        //Любой сообщение приходит сюда
        async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            switch (update.Type)
            {
                //только сообщение от клиента
                case UpdateType.Message:
                    var text = update.Message.Text;
                    await Console.Out.WriteLineAsync(text);
                    var msgChatId = update.Message.Chat.Id;

                    //проверка клинета
                    var checkClient = _clientStates.ContainsKey(msgChatId);
                    if (!checkClient)
                    {
                        //добавление пользователя
                        var user = new UserState()
                        {
                            Products = new(),
                            CashProducts = new(),
                            HistoryProducts = new(),
                            FullName = update.Message.Chat.FirstName
                        };
                        _clientStates.Add(msgChatId, user);
                    }

                    var clientMessage = _clientStates[msgChatId];
                    {
                        switch (text)
                        {
                            case Start:
                                // опять передаем клавиатуру в параметр replyMarkup
                                await botClient.SendTextMessageAsync(
                                                                  msgChatId,
                                                                  "Добро пожаловать в наш магазин!",
                                                                  replyMarkup: GetButtons());
                                break;
                            case Product:


                                await _client.SendPhotoAsync(
                                    update.Message.Chat.Id,
                                    InputFile.FromUri("https://proza.ru/pics/2020/10/31/819.jpg"),
                                    caption: Tomato,
                                    replyMarkup: GetInlineButton(Tomato, 0));

                                await _client.SendPhotoAsync(
                                    update.Message.Chat.Id,
                                    InputFile.FromUri("https://prosad.ru/wp-content/uploads/loaded/ba5d82d833a4b90083f7b362e7.jpg"),
                                    caption: Cabbage,
                                    replyMarkup: GetInlineButton(Cabbage, 0));

                                break;
                            case Basket:
                                var message = "Корзина: для заказа нажмите кнопку Заказать:\n";
                                foreach (var item in clientMessage.Products)
                                {
                                    message += $"{item.Key} - {item.Value}\n";
                                }

                                await botClient.SendTextMessageAsync(msgChatId, message, replyMarkup: GetOrderInlineButton());
                                break;

                            //case Order:
                            //    if (clientMessage.Products.Count == 0)
                            //    {
                            //        await botClient.SendTextMessageAsync(msgChatId, "Сначала выберите продукты");
                            //        return;
                            //    }
                            //    foreach (var item in clientMessage.Products)
                            //    {
                            //        clientMessage.HistoryProducts.Add(item.Key, item.Value);
                            //    }

                            //    clientMessage.Products.Clear();
                            //    clientMessage.CashProducts.Clear();

                            //    await botClient.SendTextMessageAsync(msgChatId, "Ваш заказ принят, ожидайте доставку");
                            //    break;

                            case History:
                                var messageHistory = "История заказов:\n";
                                var i = 0;
                                foreach (var items in clientMessage.HistoryProducts)
                                {
                                    messageHistory += $"{++i})";
                                    foreach (var item in items)
                                    {
                                        messageHistory += $"{item.Key} - {item.Value}\n";
                                    }
                                }

                                await botClient.SendTextMessageAsync(msgChatId, messageHistory);
                                break;

                            default:
                                await botClient.SendTextMessageAsync(
                                                                  msgChatId,
                                                                  "Чтобы начать нажмите '/start'");
                                break;
                        }
                    }
                    break;

                //для кнопки
                case UpdateType.CallbackQuery:
                    var callChatId = update.CallbackQuery!.Message!.Chat.Id;
                    var callMessageId = update.CallbackQuery.Message.MessageId;

                    var client = _clientStates[callChatId];

                    var productName = update.CallbackQuery.Message.ReplyMarkup!.InlineKeyboard
                        .Where(x => x.Count() == 1)
                        .Select(x => x.Select(x => x.CallbackData))
                        .FirstOrDefault()?
                        .FirstOrDefault();

                    var counter = update.CallbackQuery.Message.ReplyMarkup!.InlineKeyboard
                        .Where(x => x.Count() > 1)
                        .Select(x => x.Where(x => x.CallbackData == Counter).Select(x => int.Parse(x.Text)))
                        .FirstOrDefault()?
                        .FirstOrDefault();


                    switch (update.CallbackQuery.Data)
                    {
                        //case 
                        case Minus:
                            {

                                if (!client.CashProducts.ContainsKey(productName))
                                    return;

                                var count = client.CashProducts[productName];
                                if (count == 0)
                                    return;

                                counter--;
                                client.CashProducts[productName] = counter.Value;

                                await _client.EditMessageReplyMarkupAsync(callChatId, callMessageId,
                                    replyMarkup: GetInlineButton(productName, counter.Value));
                            }
                            break;
                        case Plus:
                            {
                                counter++;
                                if (!client.CashProducts.ContainsKey(productName))
                                {
                                    client.CashProducts.Add(productName, counter.Value);
                                }
                                else
                                {
                                    client.CashProducts[productName] = counter.Value;
                                }
                                await _client.EditMessageReplyMarkupAsync(callChatId, callMessageId,
                                    replyMarkup: GetInlineButton(productName, counter.Value));
                            }
                            break;
                        case Tomato:
                            {
                                if (!client.CashProducts.ContainsKey(Tomato) && client.CashProducts[Tomato] > 0)
                                {
                                    await _client.SendTextMessageAsync(callChatId, $"Сначала добавьте в корзину хотя бя один продукт '+'");
                                }
                                var countTomatoProduct = client.CashProducts[Tomato];

                                if (client.Products.ContainsKey(Tomato))
                                {
                                    client.Products[Tomato] = countTomatoProduct;
                                }
                                else
                                {
                                    client.Products.Add(Tomato, countTomatoProduct);
                                }

                                await _client.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id, $" {update.CallbackQuery.Data} - {countTomatoProduct} добавлен в корзину", replyMarkup: GetButtons());
                            }

                            break;
                        case Cabbage:
                            {
                                if (!client.CashProducts.ContainsKey(Cabbage) && client.CashProducts[Cabbage] > 0)
                                {
                                    await _client.SendTextMessageAsync(callChatId, $"Сначала добавьте в корзину хотя бя один продукт '+'");
                                }
                                var countCabbageProduct = client.CashProducts[Cabbage];

                                if (client.Products.ContainsKey(Cabbage))
                                {
                                    client.Products[Cabbage] = countCabbageProduct;
                                }
                                else
                                {
                                    client.Products.Add(Cabbage, countCabbageProduct);
                                }

                                await _client.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id, $" {update.CallbackQuery.Data} - {countCabbageProduct} добавлен в корзину", replyMarkup: GetButtons());
                            }

                            break;

                        case Order:
                            if (client.Products.Count == 0)
                            {
                                await botClient.SendTextMessageAsync(callChatId, "Сначала выберите продукты");
                                return;
                            }
                            var dic = new Dictionary<string, int>();
                            foreach (var item in client.Products)
                            {
                                dic.Add(item.Key, item.Value);
                            }
                            client.HistoryProducts.Add(dic);
                            client.Products.Clear();
                            client.CashProducts.Clear();

                            await botClient.SendTextMessageAsync(callChatId, "Спасибо за ваш заказ!");
                            break;

                    }
                    break;
                default:
                    Console.WriteLine(update.Type + " Not ipmlemented!");
                    break;
            }
        }

        //ошибка
        Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine(exception.Message);
            return Task.CompletedTask;
        }


        private InlineKeyboardMarkup GetInlineButton(string product, int counter)
        {
            return new InlineKeyboardMarkup(
                // keyboard
                new[]
                {
                    // first row
                    new[]
                    {
                        // first button in row
                         InlineKeyboardButton.WithCallbackData( "-", Minus),
                        // second button in row
                        InlineKeyboardButton.WithCallbackData( $"{counter}", Counter),
                        InlineKeyboardButton.WithCallbackData( "+", Plus )
                    },
                    // second row
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Заказать", product)
                    },

                });


        }
        private InlineKeyboardMarkup GetOrderInlineButton()
        {
            return new InlineKeyboardMarkup(
                // keyboard
                new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(Order)
                    },

                });


        }

        private IReplyMarkup GetButtons()
        {
            return new ReplyKeyboardMarkup(
                keyboard: new List<List<KeyboardButton>>
                                {
                                    new List<KeyboardButton>
                                    {
                                        new KeyboardButton(Product),
                                        new KeyboardButton(Basket),
                                        //new KeyboardButton(Order),
                                        new KeyboardButton(History),
                                    }
                                })
            {
                ResizeKeyboard = true
            };
        }

        ~TelegraBotHelper()
        {
            _srcToken.Dispose();
        }
    }
}
