using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TCS2010NX.Data;
using TCS2010NX.Models;
using MimeKit;
using MailKit.Net.Smtp;
using System.IO;
using System.IO.Compression;
using System.Net.Mime;

namespace TCS2010NX.Areas.Staff
{
    [Area("Staff")]
    public class ContributionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ContributionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Staff/Contributions
        public async Task<IActionResult> Index(int topicId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var contributions = await _context.Contribution
                .Include(c => c.Contributor)
                .Include(c => c.Topic)
                .Where(c => c.TopicId == topicId).ToListAsync();

            ViewData["TopicId"] = topicId;

            var role = await _context.UserRoles.FirstOrDefaultAsync(u => u.UserId == userId);
            if (role != null  && contributions != null)
            {
                if (role.RoleId == "Coordinator")
                {
                    var user = await _context.Users.FindAsync(userId);
                    contributions = contributions.Where(c => c.Contributor.DepartmentId == user.DepartmentId).ToList();

                    return View(contributions);
                }
                if (role.RoleId == "Manager")
                {
                    contributions = contributions.Where(c => c.Status == ContributionStatus.Approved).ToList();

                    return View(contributions);
                }
            }
            return View(contributions);
        }

        // GET: Staff/Contributions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var contribution = await _context.Contribution
                .Include(c => c.Files)
                .Include(c => c.Contributor)
                .Include(c => c.Topic)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (contribution == null)
            {
                return NotFound();
            }

            var comments = await _context.Comment.Include(c => c.User)
                                                 .Where(c => c.ContributionId == id)
                                                 .OrderBy(c => c.Date)
                                                 .ToListAsync();

            ViewData["Comments"] = comments;

            return View(contribution);
        }

        // GET: Staff/Contributions/Create

        public IActionResult Create()
        {
            ViewData["ContributorId"] = new SelectList(_context.Users, "Id", "Id");
            ViewData["TopicId"] = new SelectList(_context.Topic, "Id", "Id");
            return View();
        }

        // POST: Staff/Contributions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Status,ContributorId,TopicId")] Contribution contribution)
        {
            if (ModelState.IsValid)
            {
                _context.Add(contribution);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ContributorId"] = new SelectList(_context.Users, "Id", "Id", contribution.ContributorId);
            ViewData["TopicId"] = new SelectList(_context.Topic, "Id", "Id", contribution.TopicId);
            return View(contribution);
        }

        // GET: Staff/Contributions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var contribution = await _context.Contribution.FindAsync(id);
            if (contribution == null)
            {
                return NotFound();
            }
            ViewData["ContributorId"] = new SelectList(_context.Users, "Id", "Id", contribution.ContributorId);
            ViewData["TopicId"] = new SelectList(_context.Topic, "Id", "Id", contribution.TopicId);
            return View(contribution);
        }

        // POST: Staff/Contributions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Status,ContributorId,TopicId")] Contribution contribution)
        {
            if (id != contribution.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(contribution);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ContributionExists(contribution.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["ContributorId"] = new SelectList(_context.Users, "Id", "Id", contribution.ContributorId);
            ViewData["TopicId"] = new SelectList(_context.Topic, "Id", "Id", contribution.TopicId);
            return View(contribution);
        }

        // GET: Staff/Contributions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var contribution = await _context.Contribution
                .Include(c => c.Contributor)
                .Include(c => c.Topic)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (contribution == null)
            {
                return NotFound();
            }

            return View(contribution);
        }
        public async Task<IActionResult> DownloadFile(int fileId = -1)
        {
            var file = await _context.File.FindAsync(fileId);
            byte[] fileBytes = System.IO.File.ReadAllBytes(file.URL);

            return File(fileBytes, System.Net.Mime.MediaTypeNames.Application.Octet, file.URL.Split("\\").Last());
        }







        public async Task<IActionResult> DownloadApprovedFile(int topicId = -1)
        {
            var approvedContributions = await _context.Contribution.Include(c => c.Contributor)
                                                                   .Include(c => c.Files)
                                                                   .Where(c => c.TopicId ==
                                                                  topicId && c.Status ==
                                                                  ContributionStatus.Approved).
                                                                   ToListAsync();
            if (approvedContributions.Count() > 0)
            {
                var topic = await _context.Topic.FindAsync(topicId);
                var zipPath = Path.Combine(_Global.PATH_TOPIC, topicId.ToString(), topic.Title +
                ".zip");
                using (FileStream zipToOpen = new FileStream(zipPath, FileMode.Create))
                {
                    using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
                    {
                        foreach (var contribution in approvedContributions)
                            foreach (var file in contribution.Files)
                                archive.CreateEntryFromFile(file.URL, Path.Combine(contribution.
                                    Contributor.Number, Path.GetFileName(file.URL)));
                    }
                }
                byte[] fileBytes = System.IO.File.ReadAllBytes(zipPath);
                System.IO.File.Delete(zipPath);
                return File(fileBytes, MediaTypeNames.Application.Zip, Path.GetFileName(zipPath));
            }
            return NoContent();
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Comment(int contributionId, string commentContent)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (ModelState.IsValid)
            {
                var user = await _context.Users.FindAsync(userId);
                var existContribution = await _context.Contribution.FindAsync(contributionId);
                if (existContribution != null && !String.IsNullOrEmpty(commentContent))
                {
                    var comment = new Comment();

                    comment.UserId = userId;
                    comment.Content = commentContent;
                    comment.Date = DateTime.Now;
                    comment.ContributionId = existContribution.Id;

                    _context.Add(comment);
                    await _context.SaveChangesAsync();

                }


            }
            return RedirectToAction(nameof(Details), new { id = contributionId });
        }
        // POST: Staff/Contributions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var contribution = await _context.Contribution.FindAsync(id);
            _context.Contribution.Remove(contribution);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ContributionExists(int id)
        {
            return _context.Contribution.Any(e => e.Id == id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Mark(int contributionId = -1,
            ContributionStatus contributionStatus = ContributionStatus.Pending)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var contribution = await _context.Contribution.Include(c => c.Contributor)
                                                                  .Include(c => c.Topic)
                                                                  .FirstOrDefaultAsync(c => c.Id == contributionId);

                    contribution.Status = contributionStatus;

                    _context.Update(contribution);
                    await _context.SaveChangesAsync();

                    var contributorFullname = $"{contribution.Contributor.FirstName} {contribution.Contributor.LastName}";

                    MailboxAddress from = new MailboxAddress("iMarketing system", "vinh23189.vl@gmail.com");
                    MailboxAddress to = new MailboxAddress(contributorFullname, contribution.Contributor.Email);

                    BodyBuilder bodyBuilder = new BodyBuilder();
                    bodyBuilder.TextBody = $"Hello {contributorFullname},\n\n" +
                                           $"Thanks you for your contribution,\n\n" +
                                           $"Best regards,";

                    MimeMessage message = new MimeMessage();
                    message.From.Add(from);
                    message.To.Add(to);
                    message.Subject = $"Contribution for {contribution.Topic.Title} Status";
                    message.Body = bodyBuilder.ToMessageBody();

                    SmtpClient client = new SmtpClient();
                    client.Connect("smtp.gmail.com", 465, true);
                    client.Authenticate("vinh23189.vl", "vinhle23189");

                    client.Send(message);
                    client.Disconnect(true);
                    client.Dispose();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ContributionExists(contributionId)) { return NotFound(); }
                    else { throw;  }
                }
            }

            return RedirectToAction(nameof(Details), new { id = contributionId });
        }
    }
}






