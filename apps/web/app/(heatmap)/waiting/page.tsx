import { redirect } from 'next/navigation';

export default function WaitingLegacyRedirect() {
  redirect('/commuter/waiting');
}
