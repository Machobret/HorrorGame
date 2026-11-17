using HorrorEngine;
using TMPro;
using UnityEngine;

namespace HorrorGame.UI
{
    public sealed class InventoryFileAnnotation : MonoBehaviour
    {
        public UIDocumentEntry Entry;
        public TMP_Text Title;
        public GameObject Unread;
        void LateUpdate()
        {
            var data=Entry.Data;
            Title.gameObject.SetActive(data);
            if(data)Title.text=data.Name;
            Unread.SetActive(data && GameManager.Exists && !GameManager.Instance.Inventory.ReadDocuments.Contains(data));
        }
    }
}
