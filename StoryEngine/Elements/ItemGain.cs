using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ItemGain : Element
{
    
    private string _itemId;
    private bool _showAlert;

    public ItemGain(string itemId, bool showAlert = true)
    {
        _itemId = itemId;
        _showAlert = showAlert;
    }

    public override async UniTask ExecuteAsync(StoryContext ctx, CancellationToken token)
    {
        var itemManager = ctx.Items;

        ItemData item = itemManager.GetItemDataFromAll(_itemId);

        if (item == null)
        {
            Debug.LogError($"[ItemGain] No item with id '{_itemId}'.");
            return;
        }

        // Granted first, so a cancelled alert cannot lose the item.
        itemManager.AddItem(item.ItemID);
        
        if (_showAlert)
        {
            if(item.IsPhoto)
            {
                await new CameraFlash().ExecuteAsync(ctx, token);
            }
            await ctx.ItemUI.ShowItemGainAlertWindow(item.ItemID).AttachExternalCancellation(token);
        }
    }

    public override void ExecuteInstantly(StoryContext ctx)
    {
    }
    // Nothing to tear down: the item is already in the save, and the alert window is
    // the UI manager's to close.
    public override void ExitOnDestroy()
    {
    }
}
