using PluginAPI.Core.Attributes;
using PluginAPI.Events;

namespace DogHunt
{
    public class Plugin
    {
        [PluginEntryPoint("DogHunt", "0.5.0", "Dog searches for players around heavy", "BorkoAXT")]

        public void Setup()
        {
            EventManager.RegisterEvents(this);
        }
    }
}