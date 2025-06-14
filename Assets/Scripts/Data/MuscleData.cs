// Assets/Scripts/Data/MuscleData.cs
using System;
using Keiwando.JSON;

[Serializable]
public struct MuscleData: IJsonConvertible {

	public int id;
	public int startBoneID;
	public int endBoneID;
    public int startJointID; // Added for joint-to-joint muscle
    public int endJointID;   // Added for joint-to-joint muscle
    public bool isAttachedToJoints; // Added to differentiate connection type
	public float strength;
	public bool canExpand;
	public string userId;

    public MuscleData(int id, int startBoneID, int endBoneID, float strength, bool canExpand, string userId) {
        this.id = id;
        this.startBoneID = startBoneID;
        this.endBoneID = endBoneID;
        this.startJointID = -1; // Default invalid ID
        this.endJointID = -1;   // Default invalid ID
        this.isAttachedToJoints = false;
        this.strength = strength;
        this.canExpand = canExpand;
        this.userId = userId;
    }

    // New constructor for joint-to-joint muscles
    public MuscleData(int id, int startJointID, int endJointID, float strength, bool canExpand, string userId, bool isAttachedToJoints) {
        this.id = id;
        this.startBoneID = -1; // Default invalid ID
        this.endBoneID = -1;   // Default invalid ID
        this.startJointID = startJointID;
        this.endJointID = endJointID;
        this.isAttachedToJoints = isAttachedToJoints;
        this.strength = strength;
        this.canExpand = canExpand;
        this.userId = userId;
    }

	public JObject ToJson() {
		JObject json = new JObject();
		json.Add("id", id);
        if (isAttachedToJoints) {
            json.Add("startJointID", startJointID);
            json.Add("endJointID", endJointID);
            json.Add("isAttachedToJoints", isAttachedToJoints);
        } else {
            json.Add("startBoneID", startBoneID);
		    json.Add("endBoneID", endBoneID);
        }
		json.Add("strength", strength);
		json.Add("canExpand", canExpand);
		json.Add("userId", userId);
		return json;
	}

	public static MuscleData FromJson(JObject json) {
        int id = json.Get<JNumber>("id");
        float strength = json.Get<JNumber>("strength");
        bool canExpand = json.Get<JBoolean>("canExpand");
        string userId = json.Get<JString>("userId");

        // Handle legacy format without isAttachedToJoints
        bool isAttachedToJoints = json.Get<JBoolean>("isAttachedToJoints", false);

        if (isAttachedToJoints) {
            int startJointID = json.Get<JNumber>("startJointID");
            int endJointID = json.Get<JNumber>("endJointID");
            return new MuscleData(id, startJointID, endJointID, strength, canExpand, userId, true);
        } else {
            int startBoneID = json.Get<JNumber>("startBoneID");
            int endBoneID = json.Get<JNumber>("endBoneID");
            return new MuscleData(id, startBoneID, endBoneID, strength, canExpand, userId);
        }
	}
}
