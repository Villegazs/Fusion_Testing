using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

public class BlockManager : NetworkBehaviour
{
    private const int MaxDisabledBlocks = 4 * 3;
    [SerializeField] private float reappearTime = 3f;
    [Networked, Capacity(MaxDisabledBlocks)] private NetworkArray<Block> DisabledBlocks { get;}

    private ChangeDetector changeDetector;
    private int head;
    private int tail;
    private TickTimer tickTimer;

    public override void Spawned()
    {
        Runner.SetIsSimulated(Object, true); //Ensure that the blockManagers fixedUpdateNetwork is invoked
        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            Block tailBlock = DisabledBlocks.Get(tail);
            while (tailBlock != null && tailBlock.ReappearTick <= Runner.Tick) // Reappear tick is older than the current tick
            {
                tailBlock.Hide(false);
                DisabledBlocks.Set(tail, null);
                tail = (tail + 1) % MaxDisabledBlocks;
                tailBlock = DisabledBlocks.Get(tail);
            }
            return;
        }

        foreach (string property in changeDetector.DetectChanges(this, out NetworkBehaviourBuffer previous, out NetworkBehaviourBuffer current))
        {
            switch (property)
            {
                case nameof(DisabledBlocks):
                    ArrayReader<Block> reader = GetArrayReader<Block>(nameof(DisabledBlocks));
                    NetworkArrayReadOnly<Block> previousArray = reader.Read(previous);
                    NetworkArrayReadOnly<Block> currentArray = reader.Read(current);

                    for (int i = 0; i < MaxDisabledBlocks; i++)
                    {
                        Block currentBlock = currentArray[i];
                        Block previousBlock = previousArray[i];
                        int currentReappearTick = currentBlock == null ? -1: currentBlock.ReappearTick;
                        int previousReappearTick = previousBlock == null ? -1: previousBlock.ReappearTick;
                        if (currentReappearTick != previousReappearTick)
                        {
                            if (previousBlock !=null)
                                previousBlock .Hide(false);

                            if (currentBlock != null)
                                currentBlock.Hide(true);
                        }
                    }
                    break;

                default:
                    break;
            }
        }
    }

    public void AddDisabled(Block block)
    {
        if (!HasStateAuthority)
            return;

        Block headBlock = DisabledBlocks.Get(head);
        if (headBlock != null)
            headBlock.Hide(false);

        tickTimer = TickTimer.CreateFromSeconds(Runner, reappearTime);
        block.Hide(true);
        block.ReappearTick = tickTimer.TargetTick ?? 0;
        DisabledBlocks.Set(head, block);
        head = (head + 1) % MaxDisabledBlocks;
    }
}
