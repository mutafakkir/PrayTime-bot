using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using bot.Entity;
using bot.HttpClients;
using bot.Services;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Topten.RichTextKit;

namespace bot
{
    public class Handlers
    {
        private readonly ILogger<Handlers> _logger;
        private readonly IStorageService _storage;
        private readonly ICacheService _cache;

        public Handlers(
            ILogger<Handlers> logger, 
            IStorageService storage,
            ICacheService cache)
        {
            _logger = logger;
            _storage = storage;
            _cache = cache;
        }

        public Task HandleErrorAsync(ITelegramBotClient client, Exception exception, CancellationToken ctoken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException => $"Error occured with Telegram Client: {exception.Message}",
                _ => exception.Message
            };

            _logger.LogCritical(errorMessage);

            return Task.CompletedTask;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken ctoken)
        {
            var handler = update.Type switch
            {
                UpdateType.Message => BotOnMessageReceived(client, update.Message),
                UpdateType.EditedMessage => BotOnMessageEdited(client, update.EditedMessage),
                UpdateType.CallbackQuery => BotOnCallbackQueryReceived(client, update.CallbackQuery),
                UpdateType.InlineQuery => BotOnInlineQueryReceived(client, update.InlineQuery),
                UpdateType.ChosenInlineResult => BotOnChosenInlineResultReceived(client, update.ChosenInlineResult),
                _ => UnknownUpdateHandlerAsync(client, update)
            };

            try
            {
                await handler;
            }
            catch(Exception)
            {

            }
        }

        private async Task BotOnMessageEdited(ITelegramBotClient client, Message editedMessage)
        {
            throw new NotImplementedException();
        }

        private async Task UnknownUpdateHandlerAsync(ITelegramBotClient client, Update update)
        {
            throw new NotImplementedException();
        }

        private async Task BotOnChosenInlineResultReceived(ITelegramBotClient client, ChosenInlineResult chosenInlineResult)
        {
            throw new NotImplementedException();
        }

        private async Task BotOnInlineQueryReceived(ITelegramBotClient client, InlineQuery inlineQuery)
        {
            throw new NotImplementedException();
        }

        private async Task BotOnCallbackQueryReceived(ITelegramBotClient client, CallbackQuery callbackQuery)
        {
            throw new NotImplementedException();
        }

        private async Task BotOnMessageReceived(ITelegramBotClient client, Message message)
        {
            if(message.Type == MessageType.Location && message.Location != null)
            {
                var result = await _cache.GetOrUpdatePrayerTimeAsync(message.Chat.Id, message.Location.Longitude, message.Location.Latitude);
                var times = result.prayerTime;

                await client.SendPhotoAsync(
                    chatId:message.Chat.Id,
                    getImageFile(times, message));

                // string timeString = getTimeString(result.prayerTime);
                // await client.SendTextMessageAsync(
                //     chatId: message.Chat.Id,
                //     text: timeString,
                //     parseMode: ParseMode.Markdown);
            }

            switch(message.Text)
            {
                case "/start": 
                    if(!await _storage.ExistsAsync(message.Chat.Id))
                    {
                        var user = new BotUser(
                            chatId: message.Chat.Id,
                            username: message.From.Username,
                            fullname: $"{message.From.FirstName} {message.From.LastName}",
                            longitude: 0,
                            latitude: 0,
                            address: string.Empty);

                        var result = await _storage.InsertUserAsync(user);

                        if(result.IsSuccess)
                        {
                            _logger.LogInformation($"New user added: {message.Chat.Id}");
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"User exists");
                    }

                    await client.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        parseMode: ParseMode.Markdown,
                        text: "In order to get prayer times share your location",
                        replyMarkup: MessageBuilder.LocationRequestButton());
                    await client.DeleteMessageAsync(
                        chatId: message.Chat.Id,
                        messageId: message.MessageId);
                    break;
            }
        }

        private static Stream getImageFile(Models.PrayerTime times, Message message)
        {
            var text = getTimeString(times, message);
            using (var surface = SKSurface.Create(new SKImageInfo(1080, 1080)))
            {
                Draw(surface, text, message);
                
                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 500);
                
                return data.AsStream();
            }
        }

        private static void Draw(SKSurface surface, string text, Message message)
        {
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.ForestGreen);

            // Find the canvas bounds
            var canvasBounds = canvas.DeviceClipBounds;
            
            // Create the text block
            var tb = new TextBlock();

            // Configure layout properties
            tb.MaxWidth = canvasBounds.Width * 1f;
            tb.MaxHeight = canvasBounds.Height * 1f;
            tb.Alignment = TextAlignment.Left;

            var style = new Style()
            {
                FontFamily = "Bahnschrift",
                TextColor = SKColors.White,
                FontSize = 90
            };

            // Add text to the text block
            tb.AddText(text, style);

            // Paint the text block
            tb.Paint(canvas, new SKPoint(canvasBounds.Width * 0.19f, canvasBounds.Height * 0.17f));
        }

        private static string getTimeString(Models.PrayerTime times, Message message)
        {
                var Text = $"🌙 Fajr : {times.Fajr}\n";
                var Text1 = $"🔆 Sunrise : {times.Sunrise}\n";
                var Text2 = $"🔆 Dhuhr : {times.Dhuhr}\n";
                var Text3 = $"🔆 Asr : {times.Asr}\n";
                var Text4 = $"🌙 Maghrib : {times.Maghrib}\n";
                var Text5 = $"🌙 Isha : {times.Isha}";
                return Text + Text1 + Text2 + Text3 + Text4 + Text5;
        }
    }
}