using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Twino.MQ.Data
{
    public enum BackupOption
    {
        Copy,
        Move
    }

    public class DatabaseFile
    {
        public string Filename { get; }

        private FileStream _file;
        private DatabaseOptions _options;
        private Timer _flushTimer;

        internal bool FlushRequired { get; set; }

        public DatabaseFile(DatabaseOptions options)
        {
            _options = options;
            Filename = options.Filename;
        }

        public Stream GetStream()
        {
            return _file;
        }

        public async Task Flush()
        {
            if (_file != null)
                await _file.FlushAsync();
        }

        public async Task Open()
        {
            if (_file != null)
                return;

            _file = new FileStream(Filename, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            _file.Seek(_file.Length, SeekOrigin.Begin);

            if (_options.AutoFlush)
                await StartFlushTimer();
        }

        private async Task StartFlushTimer()
        {
            if (_flushTimer != null)
            {
                await _flushTimer.DisposeAsync();
                _flushTimer = null;
            }

            _flushTimer = new Timer(async s =>
            {
                try
                {
                    if (_file != null)
                    {
                        if (FlushRequired)
                            FlushRequired = false;

                        await _file.FlushAsync();
                    }
                }
                catch
                {
                }
            }, "", _options.FlushInterval, _options.FlushInterval);
        }

        public async Task Close()
        {
            if (_file == null)
                return;

            await _file.FlushAsync();
            await _file.DisposeAsync();
            _file = null;

            if (_flushTimer != null)
            {
                await _flushTimer.DisposeAsync();
                _flushTimer = null;
            }
        }

        public async Task<bool> Delete()
        {
            if (_file != null)
                await Close();

            try
            {
                File.Delete(Filename);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> Backup(BackupOption option)
        {
            if (_file == null)
                return false;

            try
            {
                await _file.FlushAsync();
                _file.Close();
                await _file.DisposeAsync();
                _file = null;

                if (option == BackupOption.Move)
                    File.Move(Filename, Filename + ".backup");
                else
                    File.Copy(Filename, Filename + ".backup");

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}