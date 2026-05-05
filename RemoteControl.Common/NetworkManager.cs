using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteControl.Common
{
    public class MessageConverter : JsonConverter<Message>
    {
        public override Message Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            var message = new Message();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return message;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                string propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case "Type":
                        message.Type = JsonSerializer.Deserialize<MessageType>(ref reader, options);
                        break;
                    case "Data":
                        message.Data = message.Type switch
                        {
                            MessageType.Connect => JsonSerializer.Deserialize<ConnectRequest>(ref reader, options),
                            MessageType.ConnectResponse => JsonSerializer.Deserialize<ConnectResponse>(ref reader, options),
                            MessageType.ScreenCapture => JsonSerializer.Deserialize<ScreenData>(ref reader, options),
                            MessageType.MouseEvent => JsonSerializer.Deserialize<MouseEventData>(ref reader, options),
                            MessageType.KeyboardEvent => JsonSerializer.Deserialize<KeyboardEventData>(ref reader, options),
                            MessageType.FileTransferRequest => JsonSerializer.Deserialize<FileTransferInfo>(ref reader, options),
                            MessageType.FileTransferData => JsonSerializer.Deserialize<byte[]>(ref reader, options),
                            MessageType.FileTransferResponse => JsonSerializer.Deserialize<bool>(ref reader, options),
                            _ => null
                        };
                        break;
                }
            }

            return message;
        }

        public override void Write(Utf8JsonWriter writer, Message value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Type");
            JsonSerializer.Serialize(writer, value.Type, options);
            writer.WritePropertyName("Data");

            if (value.Data != null)
            {
                JsonSerializer.Serialize(writer, value.Data, value.Data.GetType(), options);
            }
            else
            {
                writer.WriteNullValue();
            }

            writer.WriteEndObject();
        }
    }

    public class NetworkManager : IDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        public NetworkManager(TcpClient client)
        {
            _client = client;
            _stream = client.GetStream();
            _jsonOptions = new JsonSerializerOptions
            {
                Converters = { new MessageConverter() },
                ReferenceHandler = ReferenceHandler.Preserve
            };
        }

        public async Task SendMessageAsync(Message message)
        {
            string json = JsonSerializer.Serialize(message, _jsonOptions);
            byte[] data = Encoding.UTF8.GetBytes(json);
            byte[] length = BitConverter.GetBytes(data.Length);

            await _sendLock.WaitAsync();
            try
            {
                await _stream.WriteAsync(length, 0, length.Length);
                await _stream.WriteAsync(data, 0, data.Length);
                await _stream.FlushAsync();
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task<Message> ReceiveMessageAsync()
        {
            byte[] lengthBuffer = new byte[sizeof(int)];
            await ReadExactlyAsync(lengthBuffer, 0, lengthBuffer.Length);

            int length = BitConverter.ToInt32(lengthBuffer, 0);
            if (length <= 0)
            {
                throw new IOException("收到無效的封包長度。");
            }

            byte[] dataBuffer = new byte[length];
            await ReadExactlyAsync(dataBuffer, 0, dataBuffer.Length);

            string json = Encoding.UTF8.GetString(dataBuffer);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new IOException("收到空白的 JSON 封包。");
            }

            return JsonSerializer.Deserialize<Message>(json, _jsonOptions)
                ?? throw new JsonException("無法解析收到的訊息。");
        }

        private async Task ReadExactlyAsync(byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await _stream.ReadAsync(buffer, offset + totalRead, count - totalRead);
                if (read == 0)
                {
                    throw new IOException("連線已中斷。");
                }

                totalRead += read;
            }
        }

        public void Dispose()
        {
            _sendLock.Dispose();
            _stream?.Dispose();
            _client?.Dispose();
        }
    }
}
