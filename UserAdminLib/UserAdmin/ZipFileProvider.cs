﻿using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace UserAdminLib
{
    public class ZipFileSystem : IFileProvider
    {
        public ZipFileSystem(Stream stream, string rootpoath)
        {
            // make sure rootpath doesn't end with /
            if (!string.IsNullOrEmpty(rootpoath) && rootpoath.EndsWith("/")) { rootpoath = rootpoath.Substring(0, rootpoath.Length - 1); }
            this.rootpath = rootpoath;

            // unpack files
            using (var a = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var e in a.Entries)
                {
                    if (e.FullName.StartsWith(rootpoath))
                    {
                        if (e.ExternalAttributes == 32) // file ?
                        {
                            var f = new ZipFileSystem.FIleInfo(e, rootpoath);
                            allfiles[f.FullPath.ToUpper()] = f;
                        }
                        if (e.ExternalAttributes == 16)  // dir ?
                        {
                            var f = new ZipFileSystem.FIleInfo(e.FullName,rootpath, true);
                            allfiles[f.FullPath.ToUpper()] = f;
                        }
                    }
                }
            }
        }

        private readonly Dictionary<string, ZipFileSystem.FIleInfo> allfiles = new Dictionary<string, ZipFileSystem.FIleInfo>();
        private readonly string rootpath;

        IDirectoryContents IFileProvider.GetDirectoryContents(string subpath)
        {
            //lookip has uppercase
            subpath = subpath.ToUpper();

            // get files within subpath
            var files = this.allfiles.Keys.Where(_f => _f.StartsWith(subpath)).ToArray();

            //directories end with / and files have 1 dir 1 front.. filters files
            files = files.Where(_f => _f.Substring(subpath.Length).Count(_c => _c == '/') == 1).ToArray();

            // lookup fileinfo's & return
            return new DirInfo(files.Select(_f => allfiles[_f]).ToArray());
        }

        IFileInfo IFileProvider.GetFileInfo(string subpath)
        {
            if (allfiles.TryGetValue(subpath.ToUpper(), out FIleInfo file))
            {
                return file;
            }
            return new ZipFileSystem.FIleInfo(subpath,this.rootpath, false);
        }

        IChangeToken IFileProvider.Watch(string filter)
        {
            //???
            return new FileWatch();
        }

        class FileWatch : IChangeToken
        {
            bool IChangeToken.ActiveChangeCallbacks => false;
            bool IChangeToken.HasChanged => false;
            IDisposable IChangeToken.RegisterChangeCallback(Action<object> callback, object state)
            {
                throw new NotImplementedException();
            }
        }

        class DirInfo : IDirectoryContents
        {
            private FIleInfo[] content;

            public DirInfo(FIleInfo[] content)
            {
                this.content = content;
                Exists = content.Any(); // directories are there also
            }
            public bool Exists { get; }

            public IEnumerator<IFileInfo> GetEnumerator()
            {
                foreach (var f in content)
                {
                    yield return f;
                }
            }
            IEnumerator IEnumerable.GetEnumerator() => content.GetEnumerator();
        }

        class FIleInfo : IFileInfo
        {
            private readonly byte[] data;

            public FIleInfo(string path, string rootpath, bool exists)
            {
                this.Exists = exists;
                this.IsDirectory = path.EndsWith("/");
                this.LastModified = default;
                this.Length =0;
                this.Name = Path.GetFileName(this.IsDirectory?path.Substring(0,path.Length-1):path);
                this.FullPath = path.Substring(rootpath.Length);
            }
            public FIleInfo(ZipArchiveEntry e, string rootpoath)
            {
                using (var stream = e.Open())
                {
                    using (var mem = new MemoryStream())
                    {
                        stream.CopyTo(mem);
                        this.data = mem.GetBuffer();
                    }
                }
                this.Exists = true;
                this.IsDirectory = false;
                this.LastModified = e.LastWriteTime;
                this.Length = this.data.Length;
                this.Name = e.Name;
                this.FullPath = e.FullName.Substring(rootpoath.Length);
            }
            public bool Exists { get; }
            public bool IsDirectory { get; }
            public DateTimeOffset LastModified { get; }
            public long Length { get; }
            public string Name { get; }
            public string FullPath { get; }
            public string PhysicalPath => null;

            public Stream CreateReadStream()
            {
                return new MemoryStream(this.data);
            }
        }
    }
}
