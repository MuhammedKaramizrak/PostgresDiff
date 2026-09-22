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
        public static void PropagateBaseToAllNextLayers(ProjectData project, int startIndex, string objectType, string objectName)
        {
            string currentBase = null;

            for (int i = startIndex; i < project.Layers.Count; i++)
            {
                var layer = project.Layers[i];
                var obj = layer.DatabaseObjects.FirstOrDefault(o => o.ObjectType == objectType && o.ObjectName == objectName);
                if (obj == null)
                    continue;

                var selected = obj.GetSelectedOneDatabase();
                currentBase = selected?.SqlText ?? currentBase;

                if (i + 1 < project.Layers.Count)
                {
                    var nextObj = project.Layers[i + 1].DatabaseObjects
                        .FirstOrDefault(o => o.ObjectType == objectType && o.ObjectName == objectName);

                    if (nextObj != null && currentBase != null)
                    {
                        nextObj.BaseSqlText = currentBase;
                      //  nextObj.RecalculateDiffStatus(); // Varsa tetiklenir - hattaaaaaaaa çekirge RecalculateDiffStatus
                    }
                }
            }
        }
        public void HandleNotify(ProjectData project, int changedLayerIndex, string objectType, string objectName) //// 0 referans çekirge
        {
            PropagateBaseToAllNextLayers(project, changedLayerIndex, objectType, objectName);
        }

        private void OnNotification(object sender, NpgsqlNotificationEventArgs e)
        {
            NotifyReceived?.Invoke(this, e.Payload);
            var payload = e.Payload;
            var parts = payload.Split('|'); // örnek: "function|my_func"
            if (parts.Length != 2) return;

            string objectType = parts[0];
            string objectName = parts[1];
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
