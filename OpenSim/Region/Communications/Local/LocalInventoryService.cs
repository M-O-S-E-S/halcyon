/*
 * Copyright (c) InWorldz Halcyon Developers
 * Copyright (c) Contributors, http://opensimulator.org/
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.Collections.Generic;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;

namespace OpenSim.Region.Communications.Local
{
    /// <summary>
    /// An implementation of user inventory where the inventory is held locally (e.g. when OpenSim is
    /// operating in standalone mode.
    /// </summary>
    public class LocalInventoryService : InventoryServiceBase
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public override void RequestInventoryForUser(UUID userID, InventoryReceiptCallback callback)
        {
            m_log.InfoFormat("[LOCAL INVENTORY SERVICE]: Requesting inventory for user {0}", userID);

            List<InventoryFolderBase> skeletonFolders = GetInventorySkeleton(userID);
            if (skeletonFolders == null)
                return;

            InventoryFolderImpl rootFolder = null;

            List<InventoryFolderImpl> folders = new List<InventoryFolderImpl>();
            List<InventoryItemBase> allItems = RequestAllItems(userID);
            Dictionary<UUID, InventoryFolderImpl> sortedFolders = new Dictionary<UUID,InventoryFolderImpl>();
            List<InventoryItemBase> folderItems = new List<InventoryItemBase>();

            // Need to retrieve the root folder on the first pass
            foreach (InventoryFolderBase folder in skeletonFolders)
            {
                if (folder.ParentID == UUID.Zero)
                {
                    rootFolder = new InventoryFolderImpl(folder);
                    folders.Add(rootFolder);
                    sortedFolders.Add(folder.ID, rootFolder);
                    //items.AddRange(RequestFolderItems(rootFolder.ID));
                    break; // Only 1 root folder per user
                }
            }

            if (rootFolder != null)
            {
                foreach (InventoryFolderBase folder in skeletonFolders)
                {
                    if (folder.ID != rootFolder.ID)
                    {
                        InventoryFolderImpl fimpl = new InventoryFolderImpl(folder);
                        folders.Add(fimpl);
                        sortedFolders.Add(fimpl.ID, fimpl);
                    }
                }

                foreach (InventoryItemBase item in allItems)
                {
                    if (sortedFolders.ContainsKey(item.Folder)) 
                    {
                        folderItems.Add(item);
                    }
                }
            }

            

            m_log.InfoFormat(
                "[LOCAL INVENTORY SERVICE]: Received inventory response for user {0} containing {1} folders and {2} items",
                userID, folders.Count, folderItems.Count);

            callback(folders, folderItems);
        }

        public override void RequestFolderContents(UUID userID, UUID folderId, FolderContentsCallback callback)
        {
            m_log.InfoFormat("[LOCAL INVENTORY SERVICE]: Requesting folder descendents for user {0} and folder {1}", userID, folderId);

            ICollection<InventoryItemBase> items = RequestFolderItems(folderId);
            ICollection<InventoryFolderBase> folders = RequestSubFolders(folderId);
            List<InventoryFolderImpl> implFolders = new List<InventoryFolderImpl>();

            foreach (InventoryFolderBase folder in folders)
            {
                implFolders.Add(new InventoryFolderImpl(folder));
            }

            callback(folderId, implFolders, items);
        }

        public override InventoryCollection GetRecursiveFolderContents(UUID userId, UUID folderId)
        {
            m_log.InfoFormat("[LOCAL INVENTORY SERVICE]: Processing request for folder {0} recursive contents", folderId);

            InventoryCollection invCollection = new InventoryCollection();

            List<InventoryFolderBase> folders = GetRecursiveFolderList(folderId);
            List<InventoryItemBase> items = RequestItemsInMultipleFolders(folders);

            invCollection.UserID = UUID.Zero;
            invCollection.Folders = folders;
            invCollection.Items = items;


            m_log.InfoFormat(
                "[LOCAL INVENTORY SERVICE]: Sending back recursive folder contents containing {0} folders and {1} items",
                invCollection.Folders.Count, invCollection.Items.Count);

            return invCollection;
        }

        public override bool HasInventoryForUser(UUID userID)
        {
            InventoryFolderBase root = RequestRootFolder(userID);
            if (root == null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        
    }
}
