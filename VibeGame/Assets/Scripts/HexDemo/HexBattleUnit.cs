using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HexDemo
{
    public sealed class HexBattleUnit : MonoBehaviour
    {
        public HexBattleUnitState State { get; private set; }
        public HexDeckState Deck { get; } = new();
        public bool IsAlive => State != null && State.currentHealth > 0;

        private Animator _animator;
        private CapsuleCollider _targetCollider;

        public void Initialize(HexBattleUnitState state, Animator animator, IEnumerable<HexCardDefinition> startingDeck)
        {
            State = state;
            _animator = animator;
            Deck.LoadStartingDeck(startingDeck);
            EnsureTargetCollider();
        }

        public void BeginTurn()
        {
            State.energy = State.maxEnergy;
            State.currentMovePoints = State.maxMovePoints;
            Deck.DrawCards(State.drawPerTurn);
        }

        public void EndTurn()
        {
            Deck.DiscardHand();
        }

        public bool CanPay(HexCardInstance card)
        {
            return card != null && card.definition != null && State.energy >= card.definition.energyCost;
        }

        public void SpendEnergy(int amount)
        {
            State.energy = Mathf.Max(0, State.energy - amount);
        }

        public void GainArmor(int amount)
        {
            State.armor += Mathf.Max(0, amount);
        }

        public void SpendMovePoints(int amount)
        {
            State.currentMovePoints = Mathf.Max(0, State.currentMovePoints - Mathf.Max(0, amount));
        }

        public int ApplyDamage(int amount)
        {
            int remaining = Mathf.Max(0, amount);
            int absorbed = Mathf.Min(State.armor, remaining);
            State.armor -= absorbed;
            remaining -= absorbed;
            State.currentHealth = Mathf.Max(0, State.currentHealth - remaining);
            return remaining;
        }

        public IEnumerator MoveAlongPath(HexGrid grid, List<HexAxialCoord> path, float unitYOffset, float moveSpeed, float stopDelay)
        {
            if (path == null || path.Count < 2)
                yield break;

            SetMoving(true);
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 startPos = grid.AxialToWorld(path[i - 1]) + Vector3.up * unitYOffset;
                Vector3 endPos = grid.AxialToWorld(path[i]) + Vector3.up * unitYOffset;
                FaceDirection(endPos - startPos);

                float t = 0f;
                float distance = Vector3.Distance(startPos, endPos);
                float duration = distance / Mathf.Max(0.01f, moveSpeed);
                transform.position = startPos;

                while (t < 1f)
                {
                    t += Time.deltaTime / duration;
                    transform.position = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }

                transform.position = endPos;
                State.coord = path[i];

                if (stopDelay > 0f)
                    yield return new WaitForSeconds(stopDelay);
            }

            SetMoving(false);
        }

        public void SnapTo(HexGrid grid, float unitYOffset)
        {
            transform.position = grid.AxialToWorld(State.coord) + Vector3.up * unitYOffset;
        }

        public void RefreshLabel()
        {
        }

        public Vector3 GetTargetPoint()
        {
            return transform.position + Vector3.up * 1.2f;
        }

        private void FaceDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void SetMoving(bool isMoving)
        {
            if (_animator != null && _animator.runtimeAnimatorController != null)
                _animator.SetBool("IsMoving", isMoving);
        }

        private void EnsureTargetCollider()
        {
            _targetCollider = GetComponent<CapsuleCollider>();
            if (_targetCollider != null)
            {
                _targetCollider.isTrigger = false;
                _targetCollider.center = new Vector3(0f, 1f, 0f);
                _targetCollider.radius = 0.45f;
                _targetCollider.height = 2.1f;
                return;
            }

            _targetCollider = gameObject.AddComponent<CapsuleCollider>();
            _targetCollider.isTrigger = false;
            _targetCollider.center = new Vector3(0f, 1f, 0f);
            _targetCollider.radius = 0.45f;
            _targetCollider.height = 2.1f;
        }
    }
}
