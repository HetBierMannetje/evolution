// Assets/Scripts/Creature/Body/Muscle.cs
using UnityEngine;
using System;
using System.Collections;

public class Muscle : BodyComponent {

	public struct Defaults {
		public static float MaxForce = 1500f;
	}

	private const string MATERIAL_PATH = "Materials/MuscleMaterial2";
	private const string BLUE_MATERIAL_PATH = "Materials/MuscleMaterialBlue";
	private const string INVISIBLE_MATERIAL_PATH = "Materials/MuscleMaterialInvisible";

	private const float LINE_WIDTH = 0.5f;
	private const float SPRING_STRENGTH = 1000;

	public enum MuscleAction {
		CONTRACT, EXPAND	
	}

	public MuscleData MuscleData { get; set; }

	public MuscleAction muscleAction;

    // These now refer to BodyComponent, which can be either Bone or Joint
	public BodyComponent startingBodyComponent;
	public BodyComponent endingBodyComponent;

	private SpringJoint spring;

	private LineRenderer lineRenderer;

	private Rigidbody _body;
	private Collider _collider;

	private Vector3[] linePoints = new Vector3[2];

	/// <summary>
	/// Specifies whether the muscle should contract and expand or not.
	/// </summary>
	public bool living {
		set {
			_living = value;
			if (_living) {
				ShouldShowContraction = Settings.ShowMuscleContraction;
			}
		}
		get { return _living; }
	}
	private bool _living;

	public bool ShouldShowContraction { 
		get {
			return shouldShowContraction;
		} 
		set {
			shouldShowContraction = value;
			if (!value) {
				SetRedMaterial();
				SetLineWidth(1f);
			}
		} 
	}
	private bool shouldShowContraction;

	public float currentForce = 0;

	// MARK: contraction visibility
	private Material redMaterial;
	private Material blueMaterial;
	private Material invisibleMaterial;

	private float minLineWidth = 0.5f;
	private float maxLineWidth = 1.5f;

	private Vector3 resetPosition;
	private Quaternion resetRotation;

    public static Muscle CreateFromData(MuscleData data) {
        Material muscleMaterial = Resources.Load(MATERIAL_PATH) as Material;
        Material blueMaterial = Resources.Load(BLUE_MATERIAL_PATH) as Material;
        Material invisibleMaterial = Resources.Load(INVISIBLE_MATERIAL_PATH) as Material;

        GameObject muscleEmpty = new GameObject();
        muscleEmpty.name = "Muscle";
        muscleEmpty.layer = LayerMask.NameToLayer("Creature");
        var muscle = muscleEmpty.AddComponent<Muscle>();
        muscle.AddLineRenderer();
        muscle.SetMaterial(muscleMaterial);

        muscle.MuscleData = data;

        muscle.redMaterial = muscleMaterial;
        muscle.blueMaterial = blueMaterial;
        muscle.invisibleMaterial = invisibleMaterial;

        return muscle;
    }

	public override void Start () {
		base.Start();

		resetPosition = transform.position;
		resetRotation = transform.rotation;
	}

	void Update () {

		UpdateLinePoints();
		UpdateContractionVisibility();
	}

	void FixedUpdate() {
		
		if (muscleAction == MuscleAction.CONTRACT) {
			Contract();
		} else {
			Expand();	
		}
	}

	/// <summary>
	/// Connects the gameobject to the starting and ending body components.
	/// </summary>
	public void ConnectToJoints() {

        if (startingBodyComponent == null || endingBodyComponent == null) return;

        startingBodyComponent.Connect(this);
        endingBodyComponent.Connect(this);

		// connect the muscle with a spring joint
		SpringJoint currentSpring;

        // If connected to bones (legacy or not), use the bone's rigidbody.
        // If connected to joints, use the joint's rigidbody.
        if (MuscleData.isAttachedToJoints) {
            currentSpring = startingBodyComponent.gameObject.AddComponent<SpringJoint>();
            currentSpring.connectedBody = endingBodyComponent.GetComponent<Rigidbody>();
        } else {
            // Assuming startingBodyComponent and endingBodyComponent are Bones here
            Bone startingBone = startingBodyComponent as Bone;
            Bone endingBone = endingBodyComponent as Bone;

            if (startingBone.BoneData.legacy) {
                currentSpring = startingBone.legacyWeightObj.gameObject.AddComponent<SpringJoint>();
            } else {
                currentSpring = startingBone.gameObject.AddComponent<SpringJoint>();
            }
            if (endingBone.BoneData.legacy)
                currentSpring.connectedBody = endingBone.legacyWeightObj.GetComponent<Rigidbody>();
            else 
                currentSpring.connectedBody = endingBone.GetComponent<Rigidbody>();
        }
        
        currentSpring.spring = SPRING_STRENGTH;
        currentSpring.damper = 50;
        currentSpring.minDistance = 0;
        currentSpring.maxDistance = 0;

        currentSpring.anchor = startingBodyComponent.Center;
        currentSpring.connectedAnchor = endingBodyComponent.Center;

        currentSpring.enablePreprocessing = true;
        currentSpring.enableCollision = false;

        this.spring = currentSpring;
	}

	/// <summary>
	/// Updates the current muscle force to be a percentage of the maximum force.
	/// </summary>
	/// <param name="percent">The percentage of the maximum force.</param>
	public void SetContractionForce(float percent) {

		float maxForce = MuscleData.strength;
		currentForce = Mathf.Max(0.01f, Mathf.Min(maxForce, percent * maxForce));
	}

	public void Contract() {

		if (living) {
			Contract(currentForce);
		}
	}

	public void Contract(float force) {

		var startingPoint = startingBodyComponent.Center;
		var endingPoint = endingBodyComponent.Center;

		// Apply a force on both connection joints.
		Vector3 midPoint = (startingPoint + endingPoint) / 2;

		Vector3 endingForce = (midPoint - endingPoint).normalized;
		Vector3 startingForce = (midPoint - startingPoint).normalized;

		ApplyForces(force, startingForce, endingForce);
	}

	public void Expand() {

		if (living && MuscleData.canExpand) {
			Expand(currentForce);
		}
	}

	public void Expand(float force) {

		if (!MuscleData.canExpand) return;

		var startingPoint = startingBodyComponent.Center;
		var endingPoint = endingBodyComponent.Center;

		// Apply a force on both connection joints.
		Vector3 midPoint = (startingPoint + endingPoint) / 2;

		Vector3 endingForce = (endingPoint - midPoint).normalized;
		Vector3 startingForce = (startingPoint - midPoint).normalized;

		ApplyForces(force, startingForce, endingForce);
	} 

	/// <summary>
	/// Applies the starting Force to the startingBodyComponent and endingForce to the endingBodyComponent. 
	/// force specifies the magnitude of the force.
	/// </summary>
	private void ApplyForces(float force, Vector3 startingForce, Vector3 endingForce) {

		Vector3 scaleVector = new Vector3(force, force, force);
		endingForce.Scale(scaleVector);
		startingForce.Scale(scaleVector);

		startingBodyComponent.Body.AddForceAtPosition(startingForce, startingBodyComponent.Center);
		endingBodyComponent.Body.AddForceAtPosition(endingForce, endingBodyComponent.Center);
	}

	public void AddLineRenderer(){
		
		lineRenderer = gameObject.AddComponent<LineRenderer>();
		lineRenderer.startWidth = LINE_WIDTH;
		lineRenderer.endWidth = LINE_WIDTH;
		lineRenderer.receiveShadows = false;
		lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		lineRenderer.allowOcclusionWhenDynamic = false;
		lineRenderer.sortingOrder = -1;

		lineRenderer.generateLightingData = true;
	}

	public void DeleteAndAddLineRenderer(){
		lineRenderer = gameObject.GetComponent<LineRenderer>();
	}

	public void AddCollider() {

		AddColliderToLine();
	}

	private void AddColliderToLine() {

		var startingPoint = startingBodyComponent.Center;
		var endingPoint = endingBodyComponent.Center;

		BoxCollider col = gameObject.AddComponent<BoxCollider> ();
		this._collider = col;

		// Collider is added as child object of line
		// col.transform.parent = lineRenderer.transform;
		// length of line
		float lineLength = Vector3.Distance (startingPoint, endingPoint); 
		// size of collider is set where X is length of line, Y is width of line, Z will be set as per requirement
		col.size = new Vector3 (lineLength, LINE_WIDTH, 1f); 
		Vector3 midPoint = (startingPoint + endingPoint)/2;
		// setting position of collider object
		col.transform.position = midPoint; 
		col.center = Vector3.zero;
		// Calculate the angle between startPos and endPos
		float angle = (Mathf.Abs (startingPoint.y - endingPoint.y) / Mathf.Abs (startingPoint.x - endingPoint.x));
		if ((startingPoint.y < endingPoint.y && startingPoint.x > endingPoint.x) || (endingPoint.y < startingPoint.y && endingPoint.x > startingPoint.x)) {
			angle *= -1;
		}

		angle = Mathf.Rad2Deg * Mathf.Atan (angle);
		col.transform.eulerAngles = new Vector3(0, 0, angle);

		// Add a rigidbody
		Rigidbody rBody = gameObject.AddComponent<Rigidbody>();
		this._body = rBody;
		rBody.isKinematic = true;
	}

	public void RemoveCollider() {
		DestroyImmediate(GetComponent<Rigidbody>());
		DestroyImmediate(GetComponent<BoxCollider>());
	}

	private void UpdateContractionVisibility() {

		if (!_living) return;

		if (!Settings.ShowMuscles) {
			SetInvisibleMaterial();
			return;
		}

		if (!ShouldShowContraction) { return; }

		var alpha = (float)Math.Min(1f, currentForce / Math.Max(MuscleData.strength, 0.000001));

		if (muscleAction == MuscleAction.CONTRACT) {
			SetRedMaterial();
		} else if (muscleAction == MuscleAction.EXPAND && MuscleData.canExpand) {
			SetBlueMaterial();
		}

		if (!MuscleData.canExpand && muscleAction == MuscleAction.EXPAND) {
			SetLineWidth(minLineWidth);
		} else {
			SetLineWidth(minLineWidth + alpha * (maxLineWidth - minLineWidth));
		}
	}

	/// <summary>
	/// Sets the material for the attached LineRenderer.
	/// </summary>
	public void SetMaterial(Material mat) {
		lineRenderer.material = mat;
	}

	private void SetRedMaterial() {

		if (redMaterial == null) redMaterial = Resources.Load(MATERIAL_PATH) as Material;
		lineRenderer.material = redMaterial;
	}

	private void SetBlueMaterial() {
		
		if (blueMaterial == null) blueMaterial = Resources.Load(BLUE_MATERIAL_PATH) as Material;
		lineRenderer.material = blueMaterial;
	}

	private void SetInvisibleMaterial() {
		
		if (invisibleMaterial == null) invisibleMaterial = Resources.Load(INVISIBLE_MATERIAL_PATH) as Material;
		lineRenderer.material = invisibleMaterial;
	}

	private void SetLineWidth(float width) {
		lineRenderer.widthMultiplier = width;
	}

	/// <summary>
	/// Points are flattened to 2D.
	/// </summary>
	public void SetLinePoints(Vector3 startingP, Vector3 endingP) {

		startingP.z = 0; 
		endingP.z = 0;
		SetLinePoints3D(startingP, endingP);
	}

	public void SetLinePoints3D(Vector3 startingP, Vector3 endingP) {

		linePoints[0] = startingP;
		linePoints[1] = endingP;
		lineRenderer.SetPositions(linePoints);
	}

	public void UpdateLinePoints(){

		if (startingBodyComponent == null || endingBodyComponent == null) return;

		SetLinePoints3D(startingBodyComponent.transform.position, endingBodyComponent.transform.position);
	}

	public override void PrepareForEvolution () {
		living = true;
	}

	public override int GetId() {
		return MuscleData.id;
	}
		
	/// <summary>
	/// Deletes the muscle gameObject and the sprint joint
	/// </summary>
	public override void Delete() {
		base.Delete();

		Destroy(spring);
        startingBodyComponent.Disconnect(this);
        endingBodyComponent.Disconnect(this);
		Destroy(gameObject);
	}

	/// <summary>
	/// Do not use unless you know what you're doing.
	/// </summary>
	public void DeleteWithoutDisconnecting() {

		Destroy(spring);
		Destroy(gameObject);	
	}

	public override bool Equals (object o)
	{
		return base.Equals (o) || Equals(o as Muscle);
	}

	public bool Equals(Muscle m) {

		if (m == null) return false;

        // Compare based on connected body components, regardless of type
        return (m.startingBodyComponent.Equals(startingBodyComponent) && m.endingBodyComponent.Equals(endingBodyComponent)) ||
               (m.startingBodyComponent.Equals(endingBodyComponent) && m.endingBodyComponent.Equals(startingBodyComponent));
	}

	public override int GetHashCode ()
	{
		return base.GetHashCode ();
	}

	private void OnDestroy()
	{
		Destroy(lineRenderer.material);
	}
}
