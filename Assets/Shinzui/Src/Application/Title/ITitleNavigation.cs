using System.Threading.Tasks;

namespace Shinzui.Application.Title
{
    public interface ITitleNavigation
    {
        Task LoadGameAsync(string address);
        Task LoadSceneAsync(string sceneName);
        void Quit();
    }
}
