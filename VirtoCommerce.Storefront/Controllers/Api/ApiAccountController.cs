﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.Storefront.AutoRestClients.CoreModuleApi;
using VirtoCommerce.Storefront.AutoRestClients.PlatformModuleApi;
using VirtoCommerce.Storefront.AutoRestClients.PlatformModuleApi.Models;
using VirtoCommerce.Storefront.Domain.Common;
using VirtoCommerce.Storefront.Domain.Security;
using VirtoCommerce.Storefront.Domain.Security.Notifications;
using VirtoCommerce.Storefront.Model;
using VirtoCommerce.Storefront.Model.Common;
using VirtoCommerce.Storefront.Model.Common.Events;
using VirtoCommerce.Storefront.Model.Customer;
using VirtoCommerce.Storefront.Model.Customer.Services;
using VirtoCommerce.Storefront.Model.Security;
using VirtoCommerce.Storefront.Model.Security.Events;

namespace VirtoCommerce.Storefront.Controllers.Api
{
    public class ApiAccountController : StorefrontControllerBase
    {
        private readonly IEventPublisher _publisher;
        private readonly UserManager<User> _userManager;
        private readonly IMemberService _memberService;
        private readonly IStorefrontSecurity _commerceCoreApi;
        private readonly INotifications _platformNotificationApi;
        private readonly IAuthorizationService _authorizationService;

        public ApiAccountController(IWorkContextAccessor workContextAccessor, IStorefrontUrlBuilder urlBuilder, UserManager<User> userManager, IAuthorizationService authorizationService,
        IMemberService memberService, IEventPublisher publisher, IStorefrontSecurity commerceCoreApi,
                                    INotifications platformNotificationApi)
            : base(workContextAccessor, urlBuilder)
        {
            _userManager = userManager;
            _memberService = memberService;
            _publisher = publisher;
            _commerceCoreApi = commerceCoreApi;
            _platformNotificationApi = platformNotificationApi;
            _authorizationService = authorizationService;
        }

        // GET: storefrontapi/account
        [HttpGet]
        [AllowAnonymous]
        public ActionResult GetCurrentUser()
        {       
            return Json(WorkContext.CurrentUser);
        }

        /// <summary>
        /// // GET: storefrontapi/account/{userId}
        /// </summary>
        /// <param name="contactId"></param>
        /// <returns></returns>
        [HttpGet]
        [Authorize(SecurityConstants.Permissions.CanEditOrganization)]
        public async Task<ActionResult> GetUserById([FromRoute] string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && !string.IsNullOrEmpty(user.ContactId))
            {
                var authorizationResult = await _authorizationService.AuthorizeAsync(User, user?.Contact?.Organization, CanEditOrganizationResourceAuthorizeRequirement.PolicyName);
                if (!authorizationResult.Succeeded)
                {
                    return Unauthorized();
                }
            }
            return Json(user);
        }

        // DELETE: storefrontapi/account/{userId}
        [HttpDelete]
        [Authorize(SecurityConstants.Permissions.CanEditOrganization)]
        public async Task<ActionResult> DeleteUser([FromRoute] string userId)
        {
            //TODO: Authorization check
            var result = IdentityResult.Success;
            var user = await _userManager.FindByIdAsync(userId);
            if(user != null)
            {
                var authorizationResult = await _authorizationService.AuthorizeAsync(User, user?.Contact?.Organization, CanEditOrganizationResourceAuthorizeRequirement.PolicyName);
                if (!authorizationResult.Succeeded)
                {
                    return Unauthorized();
                }

                result = await _userManager.DeleteAsync(user);
                if(result.Succeeded)
                {
                   await _publisher.Publish(new UserDeletedEvent(WorkContext, user));
                }
            }
            return Json(result);
        }

        // POST: storefrontapi/account/user
        [HttpPost]
        [Authorize(SecurityConstants.Permissions.CanEditOrganization)]
        public async Task<ActionResult> RegisterNewUser([FromBody] UserRegistration registration)
        {
            var result = IdentityResult.Success;

            TryValidateModel(registration);
           
            if (ModelState.IsValid)
            {
                //Allow to register new users only within own organization
                var authorizationResult = await _authorizationService.AuthorizeAsync(User, new Organization { Id = registration.OrganizationId }, CanEditOrganizationResourceAuthorizeRequirement.PolicyName);
                if (!authorizationResult.Succeeded)
                {
                    return Unauthorized();
                }

                var user = registration.ToUser();
                user.StoreId = WorkContext.CurrentStore.Id;
                
                result = await _userManager.CreateAsync(user, registration.Password);
                if (result.Succeeded == true)
                {
                    user = await _userManager.FindByNameAsync(user.UserName);
                    await _publisher.Publish(new UserRegisteredEvent(WorkContext, user, registration));
                }               
            }
            else
            {
                result = IdentityResult.Failed(ModelState.Values.SelectMany(x => x.Errors).Select(x => new IdentityError { Description = x.ErrorMessage }).ToArray());
            }
            return Json(result);
        }

        // POST: storefrontapi/account/invitation
        [HttpPost]
        [Authorize(SecurityConstants.Permissions.CanEditOrganization)]
        public async Task<ActionResult> CreateUserInvitation([FromBody] UsersInvitation invitation)
        {
            var result = IdentityResult.Success;
            //TODO: Implement case for invite already exist user to organization
            //Allow to invite only within own organization
            var organizationId = WorkContext.CurrentUser?.Contact?.Organization?.Id;
            foreach (var email in invitation.Emails)
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new User
                    {
                        UserName = email,
                        StoreId = WorkContext.CurrentStore.Id,
                        Roles = invitation.Roles,
                        Email = email,
                    };
                    result = await _userManager.CreateAsync(user);
                }
                if (result.Succeeded)
                {
                    user = await _userManager.FindByNameAsync(user.UserName);
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var callbackUrl = Url.Action("ConfirmInvitation", "Account", new { OrganizationId =  organizationId, user.Email, Token = token }, Request.Scheme);
                    var inviteNotification = new RegistrationInvitationNotification(WorkContext.CurrentStore.Id, WorkContext.CurrentLanguage)
                    {
                        InviteUrl = callbackUrl,
                        Sender = WorkContext.CurrentStore.Email,
                        Recipient = user.Email
                    };
                    var sendingResult = await _platformNotificationApi.SendNotificationAsync(inviteNotification.ToNotificationDto());
                    if (sendingResult.IsSuccess != true)
                    {
                        var errors = result.Errors.Concat(new IdentityError[] { new IdentityError() { Description = sendingResult.ErrorMessage } }).ToArray();
                        result = IdentityResult.Failed(errors);
                    }
                }
            }
            return Json(result);
        }

        // PUT: storefrontapi/account/organization
        [HttpPut]
        [Authorize(SecurityConstants.Permissions.CanEditOrganization)]
        public async Task<ActionResult> UpdateCustomerOrganization([FromBody] Organization organization)
        {   
            //Allow to register new users only within own organization
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, organization, CanEditOrganizationResourceAuthorizeRequirement.PolicyName);
            if (!authorizationResult.Succeeded)
            {
                return Unauthorized();
            }
            await _memberService.UpdateOrganizationAsync(organization);
            
            return Ok();
        }

        // GET: storefrontapi/account/organization/current
        [HttpGet]
        public ActionResult GetCustomerOrganization()
        {
            var result = WorkContext.CurrentUser?.Contact?.Organization;
            return Json(result);
        }

        // POST: storefrontapi/account/organization/users/search
        [HttpPost]
        [Authorize(SecurityConstants.Permissions.CanEditOrganization)]
        public async Task<ActionResult> SearchOrganizationUsersAsync([FromBody] OrganizationContactsSearchCriteria searchCriteria)
        {
            searchCriteria.OrganizationId = searchCriteria.OrganizationId ?? WorkContext.CurrentUser?.Contact?.Organization?.Id;
            //Allow to register new users only within own organization
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, new Organization { Id = searchCriteria.OrganizationId }, CanEditOrganizationResourceAuthorizeRequirement.PolicyName);
            if (!authorizationResult.Succeeded)
            {
                return Unauthorized();
            }
            if (searchCriteria.OrganizationId != null)
            {
                var contactsSearchResult = await _memberService.SearchOrganizationContactsAsync(searchCriteria);
                var userIds = contactsSearchResult.Select(x => x.SecurityAccounts.FirstOrDefault()).OfType<SecurityAccount>().Select(x=>x.Id);
                var users = new List<User>();
                foreach(var userId in userIds)
                {
                    var user = await _userManager.FindByIdAsync(userId);
                    if(user != null)
                    {
                       users.Add(user);
                    }
                }
                return Json(new { TotalCount = contactsSearchResult.TotalItemCount, Results = users });
            }
            return Ok();
        }

        // POST: storefrontapi/account/{userId}/lock
        [HttpPost]
        [Authorize(SecurityConstants.Permissions.CanEditOrganization)]
        public async Task<ActionResult> LockUser([FromRoute]string userId)
        {
            //TODO: Add authorization checks
            var result = IdentityResult.Success;
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                //Allow to register new users only within own organization
                var authorizationResult = await _authorizationService.AuthorizeAsync(User, user?.Contact?.Organization, CanEditOrganizationResourceAuthorizeRequirement.PolicyName);
                if (!authorizationResult.Succeeded)
                {
                    return Unauthorized();
                }

                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            }
            return Json(result);
        }

        // POST: storefrontapi/account/{userId}/unlock
        [HttpPost]
        [Authorize(SecurityConstants.Permissions.CanEditOrganization)]
        public async Task<ActionResult> UnlockUser([FromRoute] string userId)
        {
            //TODO: Add authorization checks
            var result = IdentityResult.Success;
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                //Allow to register new users only within own organization
                var authorizationResult = await _authorizationService.AuthorizeAsync(User, user?.Contact?.Organization, CanEditOrganizationResourceAuthorizeRequirement.PolicyName);
                if (!authorizationResult.Succeeded)
                {
                    return Unauthorized();
                }


                await _userManager.ResetAccessFailedCountAsync(user);
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MinValue);
            }
            return Json(result);
        }

        // POST: storefrontapi/account
        [HttpPost]
        public async Task<ActionResult> UpdateAccount([FromBody] UserUpdateInfo userUpdateInfo)
        {
            //TODO:Check authorization
            if (string.IsNullOrEmpty(userUpdateInfo.Id))
            {
                userUpdateInfo.Id = WorkContext.CurrentUser.Id;
            }
            if (!string.IsNullOrEmpty(userUpdateInfo.Id))
            {
                var user = await _userManager.FindByIdAsync(userUpdateInfo.Id);
                if (user != null)
                {
                    if (user.ContactId != null)
                    {
                        var contact = await _memberService.GetContactByIdAsync(user.ContactId);
                        if (contact != null)
                        {
                            contact.FirstName = userUpdateInfo.FirstName;
                            contact.LastName = userUpdateInfo.LastName;
                            await _memberService.UpdateContactAsync(contact);
                        }
                    }
                    user.Email = userUpdateInfo.Email;
                    user.Roles = userUpdateInfo.Roles;
                    await _userManager.UpdateAsync(user);
                }
            }
            return Ok();
        }

        // POST: storefrontapi/account/password
        [HttpPost]
        public async Task<ActionResult> ChangePassword([FromBody] ChangePassword formModel)
        {
            var changePassword = new ChangePasswordInfo
            {
                OldPassword = formModel.OldPassword,
                NewPassword = formModel.NewPassword,
            };

            var result = await _userManager.ChangePasswordAsync(WorkContext.CurrentUser, formModel.OldPassword, formModel.NewPassword);

            return Json(new {  result.Succeeded, Errors = result.Errors.Select(x => x.Description) });
        }

        // POST: storefrontapi/account/addresses
        [HttpPost]
        public async Task<ActionResult> UpdateAddresses([FromBody] IList<Address> addresses)
        {
            await _memberService.UpdateContactAddressesAsync(WorkContext.CurrentUser.ContactId, addresses);

            return Ok();
        }
    }
}
