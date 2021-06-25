class HandleSelectionExtension extends Autodesk.Viewing.Extension {
    constructor(viewer, options) {
        super(viewer, options);
        this._group = null;
        this._button = null;
        this._ctx = null;
    }

    load() {


        var canvas = document.getElementById('canvas');
        this._ctx = canvas.getContext('2d');

        this._ctx.fillStyle = '#ddd';
        this._ctx.fillRect(0, 0, 500, 500);

        //drawBox(ctx, {
        //    min: { x: -14.682231903076172, y: 57.76641845703125 },
        //    max: { x: -35.71241760253906, y: 38.08137893676758 }
        //});

        //drawBox(ctx, {
        //    min: { x: 44.37616729736328, y: -1.2887001037597656 },
        //    max: { x: 24.687847137451172, y: -11.131217956542969 }
        //});

        return true;
    }

    drawBox(box) {

        this._ctx.lineWidth = "1";
        this._ctx.strokeStyle = "black";
        this._ctx.beginPath();
        this._ctx.rect(box.min.x + 100, box.min.y + 100,
            (box.max.x + 100) - (box.min.x + 100),
            (box.max.y + 100) - (box.min.y + 100));
        this._ctx.stroke();
    }

    unload() {
        // Clean our UI elements if we added any
        if (this._group) {
            this._group.removeControl(this._button);
            if (this._group.getNumberOfControls() === 0) {
                this.viewer.toolbar.removeControl(this._group);
            }
        }
        console.log('HandleSelectionExtension has been unloaded');
        return true;
    }

    onToolbarCreated() {
        // Create a new toolbar group if it doesn't exist
        this._group = this.viewer.toolbar.getControl('allMyAwesomeExtensionsToolbar');
        if (!this._group) {
            this._group = new Autodesk.Viewing.UI.ControlGroup('allMyAwesomeExtensionsToolbar');
            this.viewer.toolbar.addControl(this._group);
        }

        // Add a new button to the toolbar group
        this._button = new Autodesk.Viewing.UI.Button('handleSelectionExtensionButton');
        this._button.onClick = (ev) => {


            // Get current selection
            const selection = this.viewer.getSelection();
            this.viewer.clearSelection();
            // Anything selected?
            if (selection.length > 0) {
                let isolated = [];
                // Iterate through the list of selected dbIds
                selection.forEach((dbId) => {
                    // Get properties of each dbId
                    this.viewer.getProperties(dbId, (props) => {
                        // Output properties to console
                        console.log(props);

                        //Get the bounding box of the element
                        this.viewer.select([dbId]);
                        var box = this.viewer.utilities.getBoundingBox();
                        this.drawBox(box);
                        console.log(box);

                        // Ask if want to isolate
                        //if (confirm(`Isolate ${props.name} (${props.externalId})?`)) {
                            isolated.push(dbId);
                            this.viewer.isolate(isolated);
                        //}
                    });
                });

            } else {
                // If nothing selected, restore
                this.viewer.isolate(0);
            }

        };
        this._button.setToolTip('Custom Extension Test');
        this._button.addClass('handleSelectionExtensionIcon');
        this._group.addControl(this._button);
    }
}

Autodesk.Viewing.theExtensionManager.registerExtension('HandleSelectionExtension', HandleSelectionExtension);