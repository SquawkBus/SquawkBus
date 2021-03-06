using System;
using System.IO;
using System.Security;

using SquawkBus.Authentication;
using SquawkBus.Messages;

namespace SquawkBus.Extensions.PasswordFileAuthentication
{
    public class PasswordFileAuthenticator : IAuthenticator
    {
        private readonly FileSystemWatcher _watcher;
        private PasswordManager _manager;

        public PasswordFileAuthenticator(string[] args)
        {
            if (args == null || args.Length != 1)
                throw new ArgumentException("Expected 1 argument");

            FileName = Environment.ExpandEnvironmentVariables(args[0]);

            var directory = Path.GetDirectoryName(FileName);
            if (directory == string.Empty)
                directory = ".";
            var fileName = Path.GetFileName(FileName);

            _watcher = new FileSystemWatcher(directory, fileName);
            _watcher.Changed += OnChanged;

            _manager = PasswordManager.Load(FileName);

            _watcher.EnableRaisingEvents = true;
        }

        private void OnChanged(object? sender, FileSystemEventArgs e)
        {
            Manager = PasswordManager.Load(FileName);
        }

        public string Method => "BASIC";
        public string FileName { get; }
        public PasswordManager Manager
        {
            get
            {
                lock (_watcher)
                {
                    return _manager;
                }
            }
            set
            {
                lock (_watcher)
                {
                    _manager = value;
                }
            }
        }

        public AuthenticationResponse Authenticate(Stream stream)
        {
            var reader = new DataReader(stream);
            var connectionString = reader.ReadString();
            var connectionDetails = PasswordFileConnectionDetails.Parse(connectionString);

            if (!Manager.IsValid(connectionDetails.Username, connectionDetails.Password))
                throw new SecurityException();

            return new AuthenticationResponse(
                connectionDetails.Username,
                Method,
                connectionDetails.Impersonating,
                connectionDetails.ForwardedFor,
                connectionDetails.Application);
        }
    }
}