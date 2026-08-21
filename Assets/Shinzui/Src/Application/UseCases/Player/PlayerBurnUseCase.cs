using Shinzui.Application.Interfaces;
using Shinzui.Domain.ValueObjects.FMOD;

namespace Shinzui.Application.UseCases
{
    public class PlayerBurnUseCase
    {
        private readonly IFMODSEService _seService;

        public PlayerBurnUseCase(IFMODSEService seService)
        {
            _seService = seService;
        }

        /// <summary>
        /// 蜘蛛の巣を燃やした時の効果音を再生する
        /// </summary>
        public void PlayBurningSpiderwebSound()
        {
            // TODO: 音を燃やす音に置き換える
            _seService.PlayOneShot(FMODEventPath.SE_STONE_BREAKING_SE.Reference);
        }
    }
}