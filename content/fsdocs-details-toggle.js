import { LitElement, html, css } from 'https://cdn.jsdelivr.net/gh/lit/dist@3/core/lit-core.min.js';

const detailsExpanded = !!JSON.parse(localStorage.getItem('details-expanded'));

export class DetailsToggle extends LitElement {
    static properties = {
        _detailsExpanded: { state: true, type: Boolean },
    };

    constructor() {
        super();
        this._detailsExpanded = detailsExpanded;
        document.addEventListener('detailstoggled', e => this._detailsExpanded = !!e?.detail?.expanding);
    }

    static styles = css`
      :host {
        cursor: pointer;
      }
    `;

    toggleDetails() {
        this._detailsExpanded = !this._detailsExpanded;
        localStorage.setItem('details-expanded', JSON.stringify(this._detailsExpanded));
        if (this._detailsExpanded) {
            for (const details of document.getElementsByTagName('details')) {
                details.setAttribute('open', 'true');
            }
        } else {
            for (const details of document.getElementsByTagName('details')) {
                details.removeAttribute('open');
            }
        }
        document.dispatchEvent(new CustomEvent('detailstoggled', { detail: { expanding: this._detailsExpanded } }));
    }

    render() {
        const icon = this._detailsExpanded ? 'carbon:collapse-categories' : 'carbon:expand-categories';
        const title = this._detailsExpanded ? 'Collapse details' : 'Expand details';
        return html`
            <iconify-icon width="24" height="24" icon="${icon}" title="${title}" style="color:var(--header-link-color)" @click=${this.toggleDetails}></iconify-icon>
        `;
    }
}

customElements.define('fsdocs-details-toggle', DetailsToggle);
