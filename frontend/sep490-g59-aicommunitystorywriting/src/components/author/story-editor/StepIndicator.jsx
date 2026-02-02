import { Check } from 'lucide-react';

export function StepIndicator({ currentStep, steps }) {
    return (
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', marginBottom: '2rem' }}>
            {steps.map((step, index) => (
                <div key={step.number} style={{ display: 'flex', alignItems: 'center' }}>
                    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
                        <div
                            style={{
                                width: '48px',
                                height: '48px',
                                borderRadius: '50%',
                                border: `3px solid ${currentStep >= step.number ? '#6ee7b7' : '#d1d5db'}`,
                                backgroundColor: '#ffffff',
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                fontSize: '1.125rem',
                                fontWeight: 'bold',
                                color: currentStep >= step.number ? '#6ee7b7' : '#9ca3af',
                                transition: 'all 0.3s'
                            }}
                        >
                            {currentStep > step.number ? (
                                <Check style={{ width: '24px', height: '24px' }} />
                            ) : (
                                step.number
                            )}
                        </div>
                        <div style={{
                            fontSize: '0.875rem',
                            color: currentStep === step.number ? '#333333' : '#9ca3af',
                            fontWeight: currentStep === step.number ? 600 : 400,
                            marginTop: '0.5rem',
                            whiteSpace: 'nowrap'
                        }}>
                            {step.title}
                        </div>
                    </div>
                    {index < steps.length - 1 && (
                        <div style={{
                            width: '100px',
                            height: '3px',
                            backgroundColor: currentStep > step.number ? '#6ee7b7' : '#d1d5db',
                            margin: '0 1rem',
                            marginBottom: '2rem',
                            transition: 'background-color 0.3s'
                        }} />
                    )}
                </div>
            ))}
        </div>
    );
}
