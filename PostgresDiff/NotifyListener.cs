using Newtonsoft.Json.Linq;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PostgresDiff
{
    public class NotifyListener : IDisposable
    {
        private readonly string _connectionString;
        private readonly string _channel;
        private NpgsqlConnection _connection;
        private bool _isListening;
        private CancellationTokenSource _cts;

        public event EventHandler<string> NotifyReceived;

        public NotifyListener(string connectionString, ConnectionItem connection, string channel = "pgddlchange")
        {
            _connectionString = connectionString;
            _channel = channel;
        }

        public async Task StartAsync()
        {
            await Task.Delay(0).ConfigureAwait(false);
            if (_isListening) return;
            _isListening = true;
            _cts = new CancellationTokenSource();

            Task t =  ListenPayLoad(_cts);
            
        }
        private async Task ListenPayLoad(CancellationTokenSource _cts)
           
        {
            await Task.Delay(0).ConfigureAwait(false);
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await using (_connection = new NpgsqlConnection(_connectionString))
                    {
                        await _connection.OpenAsync(_cts.Token);
                        _connection.Notification += OnNotification;

                        using var listenCmd = _connection.CreateCommand();
                        listenCmd.CommandText = $"LISTEN {_channel};";
                        await listenCmd.ExecuteNonQueryAsync(_cts.Token);

                        while (!_cts.Token.IsCancellationRequested)
                        {
                            await _connection.WaitAsync(_cts.Token);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"NotifyListener error: {ex.Message}. Reconnecting in 5s...");
                    await Task.Delay(5000, _cts.Token);
                }
            }

        }

        private void OnNotification(object sender, NpgsqlNotificationEventArgs e)
        {
            NotifyReceived?.Invoke(this, e.Payload);
        }

        public void Stop()
        {
            _isListening = false;
            _cts?.Cancel();
            _connection?.Close();
        }

        public void Dispose()
        {
            Stop();
            _connection?.Dispose();
            _cts?.Dispose();
        }
    }

}
