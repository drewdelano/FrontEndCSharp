import React from "react";

type Store = {
    EnergyLevel: number;
};

type StoreProps = {
    Store: Store;
};

const Actions = {
    SetEnergy: (_store: Store, _level: number) => { }
};

export function EnergyPanel(props: StoreProps) {
    return (
        <section className="panel side-panel">
            <h2>Energy check</h2>
            <p>Name the weather before choosing the route.</p>
            <div className="energy-grid">
                <button
                    className={props.Store.EnergyLevel === 1 ? "energy-button is-active" : "energy-button"}
                    type="button"
                    onClick={() => Actions.SetEnergy(props.Store, 1)}>Low</button>
            </div>
        </section>
    );
}
