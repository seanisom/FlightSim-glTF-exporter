﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Windows.Forms;
using Autodesk.Max;
using Autodesk.Max.Plugins;
using Newtonsoft.Json;

namespace Max2Babylon
{
    [DataContract]
    public class AnimationGroupNode
    {
        [DataMember]
        public Guid Guid { get; set; } 
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string ParentName { get; set; }

        public AnimationGroupNode(Guid _guid, string _name, string _parentName)
        {
            Guid = _guid;
            Name = _name;
            ParentName = _parentName;
        }

    }

    [DataContract]
    public class AnimationGroup
    {
        public bool IsDirty { get; private set; } = true;

        [DataMember]
        public Guid SerializedId
        {
            get { return serializedId; }
            set
            {
                if (value.Equals(SerializedId))
                    return;
                IsDirty = true;
                serializedId = value;
            }
        }
        [DataMember]
        public string Name
        {
            get { return name; }
            set
            {
                if (value.Equals(name))
                    return;
                IsDirty = true;
                name = value;
            }
        }
        public int FrameStart
        {
            get { return Tools.RoundToInt(ticksStart / (float)Loader.Global.TicksPerFrame); }
            set
            {
                if (value.Equals(FrameStart)) // property getter
                    return;
                IsDirty = true;
                ticksStart = value * Loader.Global.TicksPerFrame;
            }
        }
        public int FrameEnd
        {
            get { return Tools.RoundToInt(TicksEnd / (float)Loader.Global.TicksPerFrame); }
            set
            {
                if (value.Equals(FrameEnd)) // property getter
                    return;
                IsDirty = true;
                TicksEnd = value * Loader.Global.TicksPerFrame;
            }
        }

        [DataMember]
        public List<AnimationGroupNode> AnimationGroupNodes {get; set;}

        public IList<Guid> NodeGuids
        {
            get { return nodeGuids.AsReadOnly(); }
            set
            {
                // if the lists are equal, return early so isdirty is not touched
                if (nodeGuids.Count == value.Count)
                {
                    bool equal = true;
                    int i = 0;
                    foreach (Guid newNodeGuid in value)
                    {
                        if (!newNodeGuid.Equals(nodeGuids[i]))
                        {
                            equal = false;
                            break;
                        }
                        ++i;
                    }
                    if (equal)
                        return;
                }

                IsDirty = true;
                nodeGuids.Clear();
                nodeGuids.AddRange(value);
            }
        }

        private int ticksStart = Loader.Core.AnimRange.Start;
        private int ticksEnd = Loader.Core.AnimRange.End;

        [DataMember]
        public int TicksStart
        {
            get { return ticksStart; }
            set { ticksStart = value; }
        }

        [DataMember]
        public int TicksEnd
        {
            get { return ticksEnd; }
            set { ticksEnd = value; }
        }
        

        public const string s_DisplayNameFormat = "{0} ({1:d}, {2:d})";
        public const char s_PropertySeparator = ';';
        public const string s_PropertyFormat = "{0};{1};{2};{3}";

        private Guid serializedId = Guid.NewGuid();
        private string name = "Animation";
        // use current timeline frame range by default

        private List<Guid> nodeGuids = new List<Guid>();

        public AnimationGroup() { }
        public AnimationGroup(AnimationGroup other)
        {
            DeepCopyFrom(other);
        }
        public void DeepCopyFrom(AnimationGroup other)
        {
            serializedId = other.serializedId;
            name = other.name;
            TicksStart = other.TicksStart;
            TicksEnd = other.TicksEnd;
            nodeGuids.Clear();
            nodeGuids.AddRange(other.nodeGuids);
            IsDirty = true;
        }

        public override string ToString()
        {
            return string.Format(s_DisplayNameFormat, name, FrameStart, FrameEnd);
        }

        #region Serialization

        public string GetPropertyName() { return serializedId.ToString(); }

        public void LoadFromData(string propertyName)
        {
            if (!Guid.TryParse(propertyName, out serializedId))
                throw new Exception("Invalid ID, can't deserialize.");

            string propertiesString = string.Empty;
            if (!Loader.Core.RootNode.GetUserPropString(propertyName, ref propertiesString))
                return;

            string[] properties = propertiesString.Split(s_PropertySeparator);

            if (properties.Length < 4)
                throw new Exception("Invalid number of properties, can't deserialize.");

            // set dirty explicitly just before we start loading, set to false when loading is done
            // if any exception is thrown, it will have a correct value
            IsDirty = true;

            name = properties[0];
            if (!int.TryParse(properties[1], out ticksStart))
                throw new Exception("Failed to parse FrameStart property.");
            if (!int.TryParse(properties[2], out ticksEnd))
                throw new Exception("Failed to parse FrameEnd property.");

            if (string.IsNullOrEmpty(properties[3]))
                return;

            int numNodeIDs = properties.Length - 3;
            if (nodeGuids.Capacity < numNodeIDs) nodeGuids.Capacity = numNodeIDs;
            int numFailed = 0;

            for (int i = 0; i < numNodeIDs; ++i)
            {
                Guid guid;
                if (!Guid.TryParse(properties[3 + i], out guid))
                {
                    uint id;
                    if (!uint.TryParse(properties[3 + i], out id))
                    {
                        ++numFailed;
                        continue;
                    }
                    //node is serialized in the old way ,force the reassignation of a new Guid on
                    IINode node = Loader.Core.GetINodeByHandle(id);
                    if (node != null)
                    {
                        guid= node.GetGuid();
                    }
                }
                nodeGuids.Add(guid);
                
            }

            AnimationGroupNodes = new List<AnimationGroupNode>();
            foreach (Guid nodeGuid in nodeGuids)
            {
                var node = Tools.GetINodeByGuid(nodeGuid);
                if (node != null)
                {
                    string name = node.Name;
                    string parentName =  Tools.GetINodeByGuid(nodeGuid).ParentNode.Name;
                    AnimationGroupNode nodeData = new AnimationGroupNode(nodeGuid, name, parentName);
                    AnimationGroupNodes.Add(nodeData);
                }
            }

            if (numFailed > 0)
                throw new Exception(string.Format("Failed to parse {0} node ids.", numFailed));
            
            IsDirty = false;
        }

        public void SaveToData()
        {
            // ' ' and '=' are not allowed by max, ';' is our data separator
            if (name.Contains(' ') || name.Contains('=') || name.Contains(s_PropertySeparator))
                throw new FormatException("Invalid character(s) in animation Name: " + name + ". Spaces, equal signs and the separator '" + s_PropertySeparator + "' are not allowed.");

            string nodes = string.Join(s_PropertySeparator.ToString(), nodeGuids);
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendFormat(s_PropertyFormat, name, TicksStart, TicksEnd, nodes);

            Loader.Core.RootNode.SetStringProperty(GetPropertyName(), stringBuilder.ToString());

            IsDirty = false;
        }

        public void DeleteFromData()
        {
            Loader.Core.RootNode.DeleteProperty(GetPropertyName());
            IsDirty = true;
        }

        #endregion
    }
    
    public class AnimationGroupList : List<AnimationGroup>
    {
        const string s_AnimationListPropertyName = "babylonjs_AnimationList";

        public AnimationGroup GetAnimationGroupByName(string name)
        {
            return this.First(animationGroup => animationGroup.Name == name);
        }

        public void LoadFromData()
        {
            string[] animationPropertyNames = Loader.Core.RootNode.GetStringArrayProperty(s_AnimationListPropertyName);

            if (Capacity < animationPropertyNames.Length)
                Capacity = animationPropertyNames.Length;

            foreach (string propertyNameStr in animationPropertyNames)
            {
                AnimationGroup info = new AnimationGroup();
                info.LoadFromData(propertyNameStr);
                Add(info);
            }
        }

        public void SaveToData()
        {
            List<string> animationPropertyNameList = new List<string>();
            for(int i = 0; i < Count; ++i)
            {
                if(this[i].IsDirty)
                    this[i].SaveToData();
                animationPropertyNameList.Add(this[i].GetPropertyName());
            }
            
            Loader.Core.RootNode.SetStringArrayProperty(s_AnimationListPropertyName, animationPropertyNameList);
        }

        public void SaveToJson(string filePath, List<AnimationGroup> exportList)
        {
            File.WriteAllText(filePath, JsonConvert.SerializeObject(exportList));
        }

        public void LoadFromJson(string jsonContent, bool merge = false)
        {
            List<string> animationPropertyNameList = Loader.Core.RootNode.GetStringArrayProperty(s_AnimationListPropertyName).ToList();

            if (!merge)
            {
                animationPropertyNameList = new List<string>();
                Clear();
            }
            
            List<AnimationGroup> animationGroupsData = JsonConvert.DeserializeObject<List<AnimationGroup>>(jsonContent);

            foreach (AnimationGroup animData in animationGroupsData)
            {
                List<Guid> nodeGuids = new List<Guid>();
                
                if (animData.AnimationGroupNodes != null)
                {
                    string missingNodes= "";
                    string movedNodes = "";
                    foreach (AnimationGroupNode nodeData in animData.AnimationGroupNodes)
                    {
                        //check here if something changed between export\import
                        // a node handle is reassigned the moment the node is created
                        // it is no possible to have consistency at 100% sure between two file
                        // we need to prevent artists
                        IINode node = Loader.Core.GetINodeByName(nodeData.Name);
                        if (node == null)
                        {
                            //node is missing
                            missingNodes += nodeData.Name + "\n";
                            continue;
                        }

                        if (node.ParentNode.Name != nodeData.ParentName)
                        {
                            //node has been moved in hierarchy 
                            movedNodes += node.Name + "\n";
                            continue;
                        }

                        nodeGuids.Add(node.GetGuid());
                    }

                    if (!string.IsNullOrEmpty(movedNodes))
                    {
                        //skip restoration of evaluated animation group
                        nodeGuids = new List<Guid>();
                        MessageBox.Show(string.Format("{0} has been moved in hierarchy,{1} import skipped", movedNodes, animData.Name));
                    }

                    if (!string.IsNullOrEmpty(missingNodes))
                    {
                        //skip restoration of evaluated animation group
                        nodeGuids = new List<Guid>();
                        MessageBox.Show(string.Format("{0} does not exist,{1} import skipped", missingNodes, animData.Name));
                    }
                }

                animData.NodeGuids = nodeGuids;
                string nodes = string.Join(AnimationGroup.s_PropertySeparator.ToString(), animData.NodeGuids);
                
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendFormat(AnimationGroup.s_PropertyFormat, animData.Name, animData.TicksStart, animData.TicksEnd, nodes);

                Loader.Core.RootNode.SetStringProperty(animData.SerializedId.ToString(), stringBuilder.ToString());

            }
            
            foreach (AnimationGroup animData in animationGroupsData)
            {
                string id = animData.SerializedId.ToString();

                if (merge)
                {
                    //if json are merged check if the same animgroup is already in list
                    //and skip in that case
                    if (!animationPropertyNameList.Contains(id))
                    {
                        animationPropertyNameList.Add(animData.SerializedId.ToString());
                    }
                }
                else
                {
                    animationPropertyNameList.Add(animData.SerializedId.ToString());
                }
            }

            Loader.Core.RootNode.SetStringArrayProperty(s_AnimationListPropertyName, animationPropertyNameList);

            LoadFromData();
            
        }

        public static void SaveDataToContainers()
        {
            //on scene close automatically move serialization to affected containers
            // look on each animation group if all node are in same container
            // copy property to container

            List<IIContainerObject> containers = Tools.GetAllContainers();
            if (containers.Count<=0) return;

            AnimationGroupList animationGroupList = new AnimationGroupList();
            animationGroupList.LoadFromData();

            foreach (IIContainerObject iContainerObject in containers)
            {
                RemoveDataOnContainer(iContainerObject); //cleanup for a new serialization
                List<string> animationPropertyNameList = new List<string>();
                foreach (AnimationGroup animationGroup in animationGroupList)
                {
                    IIContainerObject containerObject = animationGroup.NodeGuids.InSameContainer();
                    if (containerObject!=null && containerObject.ContainerNode.Handle == iContainerObject.ContainerNode.Handle)
                    {
                        string prop = Loader.Core.RootNode.GetStringProperty(animationGroup.GetPropertyName(),"");
                        containerObject.ContainerNode.SetStringProperty(animationGroup.GetPropertyName(), prop);
                        animationPropertyNameList.Add(animationGroup.GetPropertyName());
                    }
                }

                if (animationPropertyNameList.Count > 0)
                {
                    iContainerObject.ContainerNode.SetStringArrayProperty(s_AnimationListPropertyName, animationPropertyNameList);
                }

            }
        }

        public static void LoadDataFromContainers()
        {
            List<IIContainerObject> containers = Tools.GetAllContainers();
            if (containers.Count<=0) return;

            foreach (IIContainerObject iContainerObject in containers)
            {
                //on container added in scene try retrieve info from containers
                string[] sceneAnimationPropertyNames = Loader.Core.RootNode.GetStringArrayProperty(s_AnimationListPropertyName);
                string[] containerAnimationPropertyNames = iContainerObject.ContainerNode.GetStringArrayProperty(s_AnimationListPropertyName);
                string[] mergedAnimationPropertyNames = sceneAnimationPropertyNames.Concat(containerAnimationPropertyNames).Distinct().ToArray();

                Loader.Core.RootNode.SetStringArrayProperty(s_AnimationListPropertyName, mergedAnimationPropertyNames);

                foreach (string propertyNameStr in containerAnimationPropertyNames)
                {
                    //copy
                    string prop = iContainerObject.ContainerNode.GetStringProperty(propertyNameStr,"");
                    Loader.Core.RootNode.SetStringProperty(propertyNameStr, prop);
                }
            }
        }

        public static void RemoveDataOnContainer(IIContainerObject containerObject)
        {
            //remove all property related to animation group 
            string[] animationPropertyNames =containerObject.ContainerNode.GetStringArrayProperty(s_AnimationListPropertyName);

            foreach (string propertyNameStr in animationPropertyNames)
            {
                containerObject.ContainerNode.DeleteProperty(propertyNameStr);
            }

            containerObject.ContainerNode.DeleteProperty(s_AnimationListPropertyName);
        }
    }
}
