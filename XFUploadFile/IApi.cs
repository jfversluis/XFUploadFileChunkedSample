using System.Net.Http;
using System.Threading.Tasks;
using Refit;
using XFUploadFile.Server;

namespace XFUploadFile
{
    public interface IApi
    {
        [Get("/UploadFile/BeginFileUpload")]
        Task<string> BeginFileUpload(string fileName);
        [Post("/UploadFile/UploadChunk")]
        Task UploadChunk(MediaChunk mediaChunk);
        [Get("/UploadFile/EndFileUpload")]
        Task EndFileUpload(string fileHandle, bool quitUpload, long fileSize);

        [Post("/UploadFile")]
        Task UploadFile(MultipartFormDataContent content);
    }
}