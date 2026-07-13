using R3;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases.Inventory;
using Shinzui.Domain.ValueObjects.FMOD;

namespace Shinzui.Application.UseCases
{
    public class PlayerDeathUseCase
    {
        private readonly SpecialItemUseCase _specialItemUseCase;
        private readonly IFMODSEService _seService;
        private readonly ReactiveProperty<bool> _isDead = new(false);

        public ReadOnlyReactiveProperty<bool> IsDead => _isDead;

        public PlayerDeathUseCase(SpecialItemUseCase specialItemUseCase, IFMODSEService seService)
        {
            _specialItemUseCase = specialItemUseCase;
            _seService = seService;
        }

        /// <summary>
        /// プレイヤーを死亡状態にする
        /// 死亡状態にする前に、特別なアイテムによる死亡回避が可能かを確認する
        /// </summary>
        /// <returns>死亡状態に成功した場合は true、それ以外は false</returns>
        public bool TryKillPlayer()
        {
            if (_isDead.CurrentValue)
            {
                return false;
            }

            if (_specialItemUseCase != null && _specialItemUseCase.TryConsumeDeathPrevention())
            {
                return false;
            }

            _isDead.Value = true;
            _seService.PlayOneShot(FMODEventPath.SE_ON_DEAD.Reference);
            return true;
        }
    }
}
