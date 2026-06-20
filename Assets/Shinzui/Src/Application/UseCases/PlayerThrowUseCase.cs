using Shinzui.Application.Interfaces;
using Shinzui.Domain.ValueObjects.FMOD;

namespace Shinzui.Application.UseCases
{
    public class PlayerThrowUseCase
    {
        private readonly IFMODSEService _seService;

        public PlayerThrowUseCase(IFMODSEService seService)
        {
            _seService = seService;
        }

        /// <summary>
        /// 石が衝突した時の効果音を再生する
        /// </summary>
        public void PlayStoneHitSound()
        {
            _seService.PlayOneShot(FMODEventPath.STONE_BREAKING_SE.Reference);
        }
    }
}
