using System.Collections.Generic;

namespace Shinzui.Domain.DomainServices.ItemSpawn
{
    /// <summary>
    /// 複数のバリデータを順次実行するコンポジットバリデータ
    /// </summary>
    public sealed class CompositePositionValidator : ISpawnPositionValidator
    {
        private readonly List<ISpawnPositionValidator> _validators = new();

        public CompositePositionValidator() { }

        public CompositePositionValidator(IEnumerable<ISpawnPositionValidator> validators)
        {
            if (validators != null)
            {
                _validators.AddRange(validators);
            }
        }

        public CompositePositionValidator Add(ISpawnPositionValidator validator)
        {
            if (validator != null)
            {
                _validators.Add(validator);
            }
            return this;
        }

        public bool ValidatePosition(in SpawnValidationContext context, out string failReason)
        {
            for (int i = 0; i < _validators.Count; i++)
            {
                if (!_validators[i].ValidatePosition(in context, out failReason))
                {
                    return false;
                }
            }

            failReason = string.Empty;
            return true;
        }
    }
}
