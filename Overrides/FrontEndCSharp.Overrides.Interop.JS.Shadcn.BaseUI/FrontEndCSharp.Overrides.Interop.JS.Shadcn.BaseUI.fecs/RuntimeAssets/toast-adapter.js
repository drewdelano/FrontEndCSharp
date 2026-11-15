import { Children, cloneElement, createElement, isValidElement } from 'react';
import { Toast } from '@base-ui/react/toast';

const toastManager = Toast.createToastManager();

function splitChildren(children) {
    const templates = {};
    const content = [];
    Children.forEach(children, child => {
        const slot = isValidElement(child) ? child.props['data-slot'] : null;
        if (typeof slot === 'string' && slot.startsWith('toast-template-')) {
            templates[slot.substring('toast-template-'.length)] = child;
        } else {
            content.push(child);
        }
    });
    return { templates, content };
}

function iconFor(type, templates, className, key) {
    const template = templates[type];
    if (!template) return null;
    const classes = [template.props.className, className].filter(Boolean).join(' ');
    return cloneElement(template, {
        key,
        'data-slot': 'toast-icon',
        className: classes || undefined
    });
}

function ToastList(props) {
    const { toasts } = Toast.useToastManager();
    return toasts.map(toastItem => {
        const icon = iconFor(toastItem.type, props.iconTemplates, props.iconClassName, `${toastItem.id}-icon`);
        return createElement(
            Toast.Root,
            {
                key: toastItem.id,
                toast: toastItem,
                'data-slot': 'toast',
                className: props.rootClassName
            },
            createElement(
                Toast.Content,
                { 'data-slot': 'toast-content', className: props.contentClassName },
                icon,
                createElement(
                    'div',
                    { className: props.textClassName },
                    createElement(Toast.Title, { 'data-slot': 'toast-title', className: props.titleClassName }),
                    createElement(Toast.Description, { 'data-slot': 'toast-description', className: props.descriptionClassName })),
                createElement(
                    Toast.Close,
                    {
                        'data-slot': 'toast-close',
                        'aria-label': 'Close toast',
                        className: props.closeClassName
                    },
                    iconFor('close', props.iconTemplates, null, `${toastItem.id}-close`))));
    });
}

export function BaseUiToastHost(props) {
    const { templates, content } = splitChildren(props.children);
    return createElement(
        Toast.Provider,
        { toastManager, timeout: props.timeout },
        content,
        createElement(
            Toast.Portal,
            { 'data-slot': 'toast-portal' },
            createElement(
                Toast.Viewport,
                { 'data-slot': 'toast-viewport', className: props.viewportClassName },
                createElement(ToastList, { ...props, iconTemplates: templates }))));
}

export function addToast(title, description, type) {
    toastManager.add({ title, description, type });
}


